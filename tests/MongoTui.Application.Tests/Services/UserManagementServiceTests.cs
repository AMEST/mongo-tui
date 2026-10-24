using AwesomeAssertions;
using FakeItEasy;
using MongoTui.Application.Abstractions.Mongo;
using MongoTui.Application.Models;
using MongoTui.Application.Services;
using MongoTui.Domain.Exceptions;
using MongoTui.Domain.Primitives;
using MongoTui.Domain.Users;

namespace MongoTui.Application.Tests.Services;

public sealed class UserManagementServiceTests
{
    private static readonly MongoConnectionTarget Target = new("mongodb://localhost:27017");
    private static readonly DatabaseName Scope = DatabaseName.Create("app");

    private readonly IMongoUserGateway _gateway = A.Fake<IMongoUserGateway>();
    private readonly UserManagementService _sut;

    public UserManagementServiceTests() => _sut = new UserManagementService(_gateway, new PasswordGenerator());

    [Fact]
    public async Task Should_MapDatabaseAndClusterRoles_When_CreateUser()
    {
        IReadOnlyList<MongoRole>? captured = null;
        A.CallTo(() => _gateway.CreateUserAsync(A<MongoConnectionTarget>._, A<DatabaseName>._, A<string>._, A<string>._, A<IReadOnlyList<MongoRole>>._, A<CancellationToken>._))
            .Invokes((MongoConnectionTarget _, DatabaseName _, string _, string _, IReadOnlyList<MongoRole> roles, CancellationToken _) => captured = roles)
            .Returns(Task.CompletedTask);
        var access = Access(
            [new DatabaseAccess(DatabaseName.Create("orders"), DatabaseAccessLevel.Read),
             new DatabaseAccess(DatabaseName.Create("billing"), DatabaseAccessLevel.DbOwner)],
            ClusterRole.ClusterMonitor, ClusterRole.ReadAnyDatabase);

        await _sut.CreateAsync(Target, Scope, "app-reader", "secret", access, CancellationToken.None);

        captured.Should().BeEquivalentTo(new[]
        {
            new MongoRole("read", "orders"),
            new MongoRole("dbOwner", "billing"),
            new MongoRole("clusterMonitor", "admin"),
            new MongoRole("readAnyDatabase", "admin"),
        });
    }

    [Fact]
    public async Task Should_MapOnlyRoot_When_AdministratorSelected()
    {
        IReadOnlyList<MongoRole>? captured = null;
        A.CallTo(() => _gateway.CreateUserAsync(A<MongoConnectionTarget>._, A<DatabaseName>._, A<string>._, A<string>._, A<IReadOnlyList<MongoRole>>._, A<CancellationToken>._))
            .Invokes((MongoConnectionTarget _, DatabaseName _, string _, string _, IReadOnlyList<MongoRole> roles, CancellationToken _) => captured = roles)
            .Returns(Task.CompletedTask);
        var access = Access(
            [new DatabaseAccess(DatabaseName.Create("orders"), DatabaseAccessLevel.ReadWrite)],
            ClusterRole.Administrator, ClusterRole.ClusterMonitor);

        await _sut.CreateAsync(Target, Scope, "root-user", "secret", access, CancellationToken.None);

        captured.Should().BeEquivalentTo(new[] { new MongoRole("root", "admin") });
    }

    [Fact]
    public async Task Should_PreserveUnknownRoles_When_UpdatingUser()
    {
        IReadOnlyList<MongoRole>? captured = null;
        A.CallTo(() => _gateway.UpdateUserAsync(A<MongoConnectionTarget>._, A<DatabaseName>._, A<string>._, A<string?>._, A<IReadOnlyList<MongoRole>>._, A<CancellationToken>._))
            .Invokes((MongoConnectionTarget _, DatabaseName _, string _, string _, IReadOnlyList<MongoRole> roles, CancellationToken _) => captured = roles)
            .Returns(Task.CompletedTask);
        var existing = new MongoUser("bob", "app",
        [
            new MongoRole("read", "orders"),
            new MongoRole("customRole", "analytics"),
        ]);
        var access = Access([new DatabaseAccess(DatabaseName.Create("orders"), DatabaseAccessLevel.ReadWrite)]);

        await _sut.UpdateAsync(Target, Scope, existing, null, access, CancellationToken.None);

        captured.Should().BeEquivalentTo(new[]
        {
            new MongoRole("readWrite", "orders"),
            new MongoRole("customRole", "analytics"),
        });
    }

    [Fact]
    public async Task Should_KeepPassword_When_EditPasswordIsEmpty()
    {
        string? capturedPassword = "not-called";
        A.CallTo(() => _gateway.UpdateUserAsync(A<MongoConnectionTarget>._, A<DatabaseName>._, A<string>._, A<string?>._, A<IReadOnlyList<MongoRole>>._, A<CancellationToken>._))
            .Invokes((MongoConnectionTarget _, DatabaseName _, string _, string? password, IReadOnlyList<MongoRole> _, CancellationToken _) => capturedPassword = password)
            .Returns(Task.CompletedTask);
        var existing = new MongoUser("bob", "app", [new MongoRole("read", "orders")]);
        var access = Access([new DatabaseAccess(DatabaseName.Create("orders"), DatabaseAccessLevel.Read)]);

        await _sut.UpdateAsync(Target, Scope, existing, string.Empty, access, CancellationToken.None);

        capturedPassword.Should().BeNull();
    }

    [Fact]
    public async Task Should_Throw_When_DeletingCurrentUser()
    {
        var act = async () => await _sut.DeleteAsync(Target, Scope, "bob", "bob", CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>();
        A.CallTo(() => _gateway.DeleteUserAsync(A<MongoConnectionTarget>._, A<DatabaseName>._, A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task Should_Throw_When_CreatingUserWithoutAccess()
    {
        var access = new UserAccessSelection([], new HashSet<ClusterRole>());

        var act = async () => await _sut.CreateAsync(Target, Scope, "bob", "secret", access, CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>();
    }

    private static UserAccessSelection Access(IReadOnlyList<DatabaseAccess> databases, params ClusterRole[] clusterRoles) =>
        new(databases, new HashSet<ClusterRole>(clusterRoles));
}
