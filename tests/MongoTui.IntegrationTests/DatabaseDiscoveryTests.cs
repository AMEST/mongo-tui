using Microsoft.Extensions.Logging.Abstractions;
using AwesomeAssertions;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoTui.Application.Models;
using MongoTui.Infrastructure.Mongo.Driver;

namespace MongoTui.IntegrationTests;

[Trait("Category", "Integration")]
public sealed class DatabaseDiscoveryTests(MongoFixture fixture) : IClassFixture<MongoFixture>
{
    [Fact]
    public async Task Should_IncludeEmptyDatabase_WhenSpecifiedInConnectionUri()
    {
        var name = $"empty_{Guid.NewGuid():N}";
        var uri = new MongoUrlBuilder(fixture.ConnectionString) { DatabaseName = name, AuthenticationSource = "admin" };
        using var gateway = new DriverMongoGateway(new MongoDatabaseDiscovery(NullLogger<MongoDatabaseDiscovery>.Instance));

        var databases = await gateway.ListDatabasesAsync(new MongoConnectionTarget(uri.ToString()), CancellationToken.None);

        databases.Should().ContainSingle(database => database.Name == name && database.IsEmpty);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Should_IncludeEmptyDatabase_WhenKnownFromUserPrivilegesOrAuthentication(bool authenticationDatabase)
    {
        var name = $"empty_{Guid.NewGuid():N}";
        var user = $"user_{Guid.NewGuid():N}";
        using var admin = new MongoClient(fixture.ConnectionString);
        var source = authenticationDatabase ? name : "admin";
        var role = new BsonDocument
        {
            { "role", authenticationDatabase ? "readWriteAnyDatabase" : "readWrite" },
            { "db", authenticationDatabase ? "admin" : name }
        };
        await admin.GetDatabase(source).RunCommandAsync<BsonDocument>(new BsonDocument
        {
            { "createUser", user }, { "pwd", "test-password" },
            { "roles", new BsonArray { role } }
        });
        var uri = new MongoUrlBuilder(fixture.ConnectionString)
        {
            Username = user, Password = "test-password", AuthenticationSource = source
        };
        using var gateway = new DriverMongoGateway(new MongoDatabaseDiscovery(NullLogger<MongoDatabaseDiscovery>.Instance));

        var databases = await gateway.ListDatabasesAsync(new MongoConnectionTarget(uri.ToString()), CancellationToken.None);

        databases.Should().ContainSingle(database => database.Name == name && database.IsEmpty);
    }

    [Fact]
    public async Task Should_KeepEmptyDatabase_WhenLastCollectionDropped()
    {
        var name = $"empty_{Guid.NewGuid():N}";
        using var admin = new MongoClient(fixture.ConnectionString);
        var database = admin.GetDatabase(name);
        await database.CreateCollectionAsync("temporary");
        await database.DropCollectionAsync("temporary");
        using var gateway = new DriverMongoGateway(new MongoDatabaseDiscovery(NullLogger<MongoDatabaseDiscovery>.Instance));
        var uri = new MongoUrlBuilder(fixture.ConnectionString) { DatabaseName = name, AuthenticationSource = "admin" };

        var databases = await gateway.ListDatabasesAsync(new MongoConnectionTarget(uri.ToString()), CancellationToken.None);

        databases.Should().ContainSingle(info => info.Name == name && info.IsEmpty);
    }
}
