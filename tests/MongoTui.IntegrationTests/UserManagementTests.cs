using Microsoft.Extensions.Logging.Abstractions;
using AwesomeAssertions;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoTui.Application.Models;
using MongoTui.Application.Services;
using MongoTui.Domain.Exceptions;
using MongoTui.Domain.Primitives;
using MongoTui.Domain.Users;
using MongoTui.Infrastructure.Mongo.Driver;

namespace MongoTui.IntegrationTests;

[Trait("Category", "Integration")]
public sealed class UserManagementTests(MongoFixture fixture) : IClassFixture<MongoFixture>
{
    [Fact]
    public async Task Should_ReturnScopedUserWithDatabaseAccess_WhenCreated()
    {
        using var gateway = new DriverMongoGateway(new MongoDatabaseDiscovery(NullLogger<MongoDatabaseDiscovery>.Instance));
        var users = new UserManagementService(gateway, new PasswordGenerator());
        var target = new MongoConnectionTarget(fixture.ConnectionString);
        var scope = NewScope();
        try
        {
            await users.CreateAsync(target, scope, "reader", "original-password", Access("orders", DatabaseAccessLevel.Read), CancellationToken.None);

            var created = (await users.ListAsync(target, scope, CancellationToken.None)).Should().ContainSingle().Subject;
            created.Username.Should().Be("reader");
            created.Database.Should().Be(scope.Value);
            created.Roles.Should().Equal(new MongoRole("read", "orders"));
        }
        finally { await gateway.DeleteUserAsync(target, scope, "reader", CancellationToken.None); }
    }

    [Fact]
    public async Task Should_KeepCustomRoleAndPassword_WhenOnlyManagedAccessChanges()
    {
        using var gateway = new DriverMongoGateway(new MongoDatabaseDiscovery(NullLogger<MongoDatabaseDiscovery>.Instance));
        using var client = new MongoClient(fixture.ConnectionString);
        var users = new UserManagementService(gateway, new PasswordGenerator());
        var target = new MongoConnectionTarget(fixture.ConnectionString);
        var scope = NewScope();
        await client.GetDatabase(scope.Value).RunCommandAsync<BsonDocument>(new BsonDocument
        { ["createRole"] = "custom_reporter", ["privileges"] = new BsonArray(), ["roles"] = new BsonArray() });
        MongoRole[] roles = [new("read", "orders"), new("custom_reporter", scope.Value)];
        await gateway.CreateUserAsync(target, scope, "reader", "original-password", roles, CancellationToken.None);
        try
        {
            await users.UpdateAsync(target, scope, new MongoUser("reader", scope.Value, roles), string.Empty,
                Access("orders", DatabaseAccessLevel.ReadWrite), CancellationToken.None);

            var changed = (await users.ListAsync(target, scope, CancellationToken.None)).Should().ContainSingle().Subject;
            changed.Roles.Should().BeEquivalentTo(new MongoRole[] { new("readWrite", "orders"), new("custom_reporter", scope.Value) });
            using var authenticated = new MongoClient(UserUri("reader", "original-password", scope));
            var ping = await authenticated.GetDatabase("admin").RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));
            ping["ok"].ToInt32().Should().Be(1);
        }
        finally { await gateway.DeleteUserAsync(target, scope, "reader", CancellationToken.None); }
    }

    [Fact]
    public async Task Should_RemoveScopedUser_WhenDeleted()
    {
        using var gateway = new DriverMongoGateway(new MongoDatabaseDiscovery(NullLogger<MongoDatabaseDiscovery>.Instance));
        var users = new UserManagementService(gateway, new PasswordGenerator());
        var target = new MongoConnectionTarget(fixture.ConnectionString);
        var scope = NewScope();
        await users.CreateAsync(target, scope, "reader", "original-password", Access("orders", DatabaseAccessLevel.Read), CancellationToken.None);

        await users.DeleteAsync(target, scope, "reader", "another-user", "admin", CancellationToken.None);

        (await users.ListAsync(target, scope, CancellationToken.None)).Should().BeEmpty();
    }

    [Fact]
    public async Task Should_ReportAlreadyExists_WhenCreatingDuplicateUser()
    {
        using var gateway = new DriverMongoGateway(new MongoDatabaseDiscovery(NullLogger<MongoDatabaseDiscovery>.Instance));
        var users = new UserManagementService(gateway, new PasswordGenerator());
        var target = new MongoConnectionTarget(fixture.ConnectionString);
        var scope = NewScope();
        await users.CreateAsync(target, scope, "reader", "original-password", Access("orders", DatabaseAccessLevel.Read), CancellationToken.None);
        try
        {
            var act = () => users.CreateAsync(target, scope, "reader", "other-password", Access("orders", DatabaseAccessLevel.Read), CancellationToken.None);

            var failure = await act.Should().ThrowAsync<UserManagementException>();
            failure.Which.Code.Should().Be(UserManagementError.AlreadyExists);
            failure.Which.InnerException.Should().BeOfType<MongoCommandException>();
        }
        finally { await gateway.DeleteUserAsync(target, scope, "reader", CancellationToken.None); }
    }

    [Fact]
    public async Task Should_ReportNotFound_WhenDeletingMissingUser()
    {
        using var gateway = new DriverMongoGateway(new MongoDatabaseDiscovery(NullLogger<MongoDatabaseDiscovery>.Instance));
        var users = new UserManagementService(gateway, new PasswordGenerator());
        var target = new MongoConnectionTarget(fixture.ConnectionString);

        var act = () => users.DeleteAsync(target, NewScope(), "missing-user", "admin", CancellationToken.None);

        var failure = await act.Should().ThrowAsync<UserManagementException>();
        failure.Which.Code.Should().Be(UserManagementError.NotFound);
        failure.Which.InnerException.Should().BeOfType<MongoCommandException>();
    }

    [Fact]
    public async Task Should_ReportInsufficientPrivileges_WhenReadOnlyUserCreatesAnotherUser()
    {
        using var gateway = new DriverMongoGateway(new MongoDatabaseDiscovery(NullLogger<MongoDatabaseDiscovery>.Instance));
        var users = new UserManagementService(gateway, new PasswordGenerator());
        var target = new MongoConnectionTarget(fixture.ConnectionString);
        var scope = NewScope();
        await users.CreateAsync(target, scope, "reader", "original-password", Access("orders", DatabaseAccessLevel.Read), CancellationToken.None);
        try
        {
            var restricted = new MongoConnectionTarget(UserUri("reader", "original-password", scope));

            var act = () => users.CreateAsync(restricted, scope, "other", "other-password", Access("orders", DatabaseAccessLevel.Read), CancellationToken.None);

            var failure = await act.Should().ThrowAsync<UserManagementException>();
            failure.Which.Code.Should().Be(UserManagementError.InsufficientPrivileges);
            failure.Which.InnerException.Should().BeOfType<MongoCommandException>();
        }
        finally { await gateway.DeleteUserAsync(target, scope, "reader", CancellationToken.None); }
    }

    private string UserUri(string username, string password, DatabaseName scope) => new MongoUrlBuilder(fixture.ConnectionString)
    { Username = username, Password = password, AuthenticationSource = scope.Value }.ToString();

    private static DatabaseName NewScope() => DatabaseName.Create($"users_{Guid.NewGuid():N}");
    private static UserAccessSelection Access(string database, DatabaseAccessLevel level) =>
        new([new DatabaseAccess(DatabaseName.Create(database), level)], new HashSet<ClusterRole>());
}
