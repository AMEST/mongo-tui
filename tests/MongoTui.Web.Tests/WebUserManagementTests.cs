using AwesomeAssertions;
using FakeItEasy;
using MongoTui.Application.Models;
using MongoTui.Domain.Exceptions;
using MongoTui.Domain.Primitives;
using MongoTui.Domain.Users;
using MongoTui.Web.Services;

namespace MongoTui.Web.Tests;

public sealed class WebUserManagementTests
{
    [Fact]
    public async Task Should_CreateScopedUserAndReturnEncodedCredentials_WhenSaved()
    {
        await using var fixture = new UserManagementFixture();
        await fixture.ConnectAsync(uri: "mongodb://localhost:27017/app?tls=true&proxyHost=127.0.0.1&proxyPort=1080");
        var draft = new UserDraft { Username = " bob@example.com ", Password = "p@:/?#[]%" };
        draft.SetDatabaseAccess("orders", DatabaseAccessLevel.ReadWrite);

        var result = await fixture.Users.SaveAsync(fixture.Selection, draft, CancellationToken.None);

        result.Credentials!.Username.Should().Be("bob@example.com");
        result.Credentials.ConnectionString.Should().Be("mongodb://bob%40example.com:p%40%3A%2F%3F%23%5B%5D%25@localhost:27017/app?tls=true&authSource=admin");
        result.ReconnectRequired.Should().BeFalse();
        result.Updated.Should().BeFalse();
        A.CallTo(() => fixture.Gateway.CreateUserAsync(A<MongoConnectionTarget>._, DatabaseName.Create("admin"),
            "bob@example.com", draft.Password, A<IReadOnlyList<MongoRole>>.That.Matches(roles => roles.Contains(new MongoRole("readWrite", "orders"))),
            A<CancellationToken>.That.Matches(token => token.CanBeCanceled))).MustHaveHappenedOnceExactly();
        A.CallTo(() => fixture.Workspace.Repository.UpdateAsync(A<StoredConnection>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task Should_KeepPasswordIdentityAndCustomRoles_WhenEditingWithoutPassword()
    {
        await using var fixture = new UserManagementFixture();
        await fixture.ConnectAsync();
        var draft = new UserDraft(new MongoUser("alice", "admin", [new("read", "orders"), new("custom", "reports")])) { Username = "renamed" };
        draft.SetDatabaseAccess("orders", DatabaseAccessLevel.DbOwner);

        var result = await fixture.Users.SaveAsync(fixture.Selection, draft, CancellationToken.None);

        result.Credentials.Should().BeNull();
        result.Updated.Should().BeTrue();
        A.CallTo(() => fixture.Gateway.UpdateUserAsync(A<MongoConnectionTarget>._, DatabaseName.Create("admin"), "alice", null,
            A<IReadOnlyList<MongoRole>>.That.Matches(roles => roles.Count == 2 && roles.Contains(new MongoRole("custom", "reports"))
                && roles.Contains(new MongoRole("dbOwner", "orders"))), A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Should_AllowRemovingAllManagedRoles_WhenEditingExistingUser()
    {
        await using var fixture = new UserManagementFixture();
        await fixture.ConnectAsync();
        var draft = new UserDraft(new MongoUser("alice", "admin", [new("read", "orders"), new("custom", "reports")]));
        draft.RemoveDatabaseAccess(DatabaseName.Create("orders"));

        await fixture.Users.SaveAsync(fixture.Selection, draft, CancellationToken.None);

        A.CallTo(() => fixture.Gateway.UpdateUserAsync(A<MongoConnectionTarget>._, DatabaseName.Create("admin"), "alice", null,
            A<IReadOnlyList<MongoRole>>.That.Matches(roles => roles.Count == 1 && roles[0].Name == "custom"), A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Theory]
    [InlineData("", "secret", true, UserManagementError.UsernameRequired)]
    [InlineData("alice", "", true, UserManagementError.PasswordRequired)]
    [InlineData("alice", "secret", false, UserManagementError.AccessRequired)]
    public async Task Should_RejectInvalidNewUser_WhenSaving(string username, string password, bool access, UserManagementError code)
    {
        await using var fixture = new UserManagementFixture();
        await fixture.ConnectAsync();
        var draft = new UserDraft { Username = username, Password = password };
        if (access) draft.SetDatabaseAccess("orders", DatabaseAccessLevel.Read);

        var act = () => fixture.Users.SaveAsync(fixture.Selection, draft, CancellationToken.None);

        await act.Should().ThrowAsync<UserManagementException>().Where(error => error.Code == code);
        A.CallTo(() => fixture.Gateway.CreateUserAsync(A<MongoConnectionTarget>._, A<DatabaseName>._, A<string>._, A<string>._,
            A<IReadOnlyList<MongoRole>>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Theory]
    [InlineData("admin", true)]
    [InlineData("orders", false)]
    public async Task Should_RequireReconnectOnlyForSameIdentity_WhenPasswordChanged(string userDatabase, bool expected)
    {
        await using var fixture = new UserManagementFixture();
        await fixture.ConnectAsync("alice");
        var selection = fixture.Selection with { Database = userDatabase };
        var draft = new UserDraft(new MongoUser("alice", userDatabase, [new("read", "orders")])) { Password = "new-secret" };

        var result = await fixture.Users.SaveAsync(selection, draft, CancellationToken.None);

        result.ReconnectRequired.Should().Be(expected);
        result.Credentials!.ConnectionString.Should().Contain($"authSource={userDatabase}");
    }

    [Fact]
    public async Task Should_BlockDeletingCurrentUser_WhenUsernameAndAuthDatabaseMatch()
    {
        await using var fixture = new UserManagementFixture();
        await fixture.ConnectAsync("alice", "orders");
        var user = new MongoUser("alice", "orders", []);

        var act = () => fixture.Users.DeleteAsync(fixture.Selection with { Database = "orders" }, user, CancellationToken.None);

        await act.Should().ThrowAsync<UserManagementException>().Where(error => error.Code == UserManagementError.DeleteCurrentForbidden);
        A.CallTo(() => fixture.Gateway.DeleteUserAsync(A<MongoConnectionTarget>._, A<DatabaseName>._, A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task Should_AllowSameUsernameInAnotherAuthDatabase_WhenDeleting()
    {
        await using var fixture = new UserManagementFixture();
        await fixture.ConnectAsync("alice", "orders");

        await fixture.Users.DeleteAsync(fixture.Selection, new MongoUser("alice", "admin", []), CancellationToken.None);

        A.CallTo(() => fixture.Gateway.DeleteUserAsync(A<MongoConnectionTarget>._, DatabaseName.Create("admin"), "alice", A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Should_CancelPendingRequest_WhenDialogTokenIsCancelled()
    {
        await using var fixture = new UserManagementFixture();
        await fixture.ConnectAsync();
        A.CallTo(() => fixture.Gateway.ListUsersAsync(A<MongoConnectionTarget>._, A<DatabaseName>._, A<CancellationToken>._))
            .ReturnsLazily(async (MongoConnectionTarget _, DatabaseName _, CancellationToken token) =>
            { await Task.Delay(Timeout.InfiniteTimeSpan, token); return (IReadOnlyList<MongoUser>)Array.Empty<MongoUser>(); });
        using var cancellation = new CancellationTokenSource();
        var running = fixture.Users.ListAsync(fixture.Selection, cancellation.Token);

        cancellation.Cancel();

        await ((Func<Task>)(() => running)).Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Should_RejectOperations_WhenConnectionWasClosed()
    {
        await using var fixture = new UserManagementFixture();
        await fixture.ConnectAsync();
        await fixture.Workspace.Catalog.DisconnectAsync(fixture.Selection.ConnectionId);

        var act = () => fixture.Users.ListAsync(fixture.Selection, CancellationToken.None);

        await act.Should().ThrowAsync<UserManagementException>().Where(error => error.Code == UserManagementError.ConnectionClosed);
    }
}
