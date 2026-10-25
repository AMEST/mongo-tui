using MongoTui.Application.Abstractions.Diagnostics;
using System.Text;
using AwesomeAssertions;
using FakeItEasy;
using MongoTui.Application.Models;
using MongoTui.Domain.Connections;
using MongoTui.Domain.Exceptions;
using MongoTui.Web.Services;

namespace MongoTui.Web.Tests;

public sealed class ConnectionEditorTests
{
    [Fact]
    public async Task Should_PreserveSecret_WhenRenamingExistingConnection()
    {
        await using var fixture = new WorkspaceFixture();
        var profile = fixture.Profile with { Auth = new AuthOptions { Kind = AuthKind.ScramSha256, Username = "alice" } };
        A.CallTo(() => fixture.Repository.GetAsync(profile.Id, A<CancellationToken>._))
            .Returns(new StoredConnection(profile, new EncryptedSecret(new byte[12], new byte[16])));
        A.CallTo(() => fixture.Crypto.Decrypt(A<byte[]>._, A<byte[]>._, A<byte[]>._))
            .Returns(Encoding.UTF8.GetBytes("retained-password"));
        byte[] nonce = [1, 2];
        byte[]? plaintext = null;
        A.CallTo(() => fixture.Crypto.Encrypt(A<byte[]>._, A<byte[]>._, out nonce))
            .Invokes(call => plaintext = call.GetArgument<byte[]>(0)!.ToArray()).Returns([3, 4]);
        var editor = new ConnectionEditor(fixture.Connections);

        await editor.SaveAsync(new ConnectionDraft { Id = profile.Id, Name = "Renamed", Uri = profile.Uri }, CancellationToken.None);

        Encoding.UTF8.GetString(plaintext!).Should().Be("retained-password");
        A.CallTo(() => fixture.Repository.UpdateAsync(A<StoredConnection>.That.Matches(c =>
            c.Profile.Name == "Renamed" && c.Profile.Auth.Username == "alice" && c.Secret != null),
            A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Should_TestDraftWithoutSaving_WhenNameIsEmpty()
    {
        await using var fixture = new WorkspaceFixture();
        var editor = new ConnectionEditor(fixture.Connections);
        var draft = new ConnectionDraft { Uri = "mongodb://bob:pw@localhost:27017/app?authSource=admin&tls=true" };

        await editor.TestAsync(draft, CancellationToken.None);

        A.CallTo(() => fixture.Gateway.ListDatabasesAsync(A<MongoConnectionTarget>.That.Matches(target =>
            target.ConnectionString == "mongodb://bob:pw@localhost:27017/app?authSource=admin&tls=true"),
            A<CancellationToken>.That.Matches(token => token.CanBeCanceled))).MustHaveHappenedOnceExactly();
        A.CallTo(() => fixture.Repository.AddAsync(A<StoredConnection>._, A<CancellationToken>._)).MustNotHaveHappened();
        A.CallTo(() => fixture.Repository.UpdateAsync(A<StoredConnection>._, A<CancellationToken>._)).MustNotHaveHappened();
        draft.Name.Should().BeEmpty();
        fixture.Catalog.Active.Should().BeEmpty();
    }

    [Theory]
    [InlineData("", "retained-password")]
    [InlineData("new-password", "new-password")]
    public async Task Should_UseEffectivePassword_WhenTestingExistingConnection(string password, string expected)
    {
        await using var fixture = new WorkspaceFixture();
        var profile = fixture.Profile with { Auth = new AuthOptions { Kind = AuthKind.ScramSha256, Username = "alice" } };
        A.CallTo(() => fixture.Repository.GetAsync(profile.Id, A<CancellationToken>._))
            .Returns(new StoredConnection(profile, new EncryptedSecret(new byte[12], new byte[16])));
        A.CallTo(() => fixture.Crypto.Decrypt(A<byte[]>._, A<byte[]>._, A<byte[]>._))
            .Returns(Encoding.UTF8.GetBytes("retained-password"));
        var editor = new ConnectionEditor(fixture.Connections);

        await editor.TestAsync(new ConnectionDraft { Id = profile.Id, Uri = profile.Uri, Password = password }, CancellationToken.None);

        A.CallTo(() => fixture.Gateway.ListDatabasesAsync(A<MongoConnectionTarget>.That.Matches(target =>
            target.ConnectionString.Contains($"alice:{expected}@")), A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        A.CallTo(() => fixture.Repository.UpdateAsync(A<StoredConnection>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task Should_PropagateFailure_WhenConnectionTestFails()
    {
        await using var fixture = new WorkspaceFixture();
        A.CallTo(() => fixture.Gateway.ListDatabasesAsync(A<MongoConnectionTarget>._, A<CancellationToken>._))
            .Throws(new ConnectionFailedException("Connection refused."));
        var editor = new ConnectionEditor(fixture.Connections);

        var act = () => editor.TestAsync(new ConnectionDraft(), CancellationToken.None);

        await act.Should().ThrowAsync<ConnectionFailedException>().WithMessage("Connection refused.");
        A.CallTo(() => fixture.Repository.AddAsync(A<StoredConnection>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task Should_CancelConnectionTest_WhenCallerCancels()
    {
        await using var fixture = new WorkspaceFixture();
        A.CallTo(() => fixture.Gateway.ListDatabasesAsync(A<MongoConnectionTarget>._, A<CancellationToken>._))
            .ReturnsLazily((MongoConnectionTarget _, CancellationToken token) => WaitForCancellationAsync(token));
        using var cancellation = new CancellationTokenSource();
        var editor = new ConnectionEditor(fixture.Connections);
        var running = editor.TestAsync(new ConnectionDraft(), cancellation.Token);

        cancellation.Cancel();

        var act = () => running;
        await act.Should().ThrowAsync<OperationCanceledException>();
        A.CallTo(() => fixture.Repository.AddAsync(A<StoredConnection>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task Should_StopConnectionTest_WhenThreeSecondDeadlineExpires()
    {
        await using var fixture = new WorkspaceFixture();
        var executionToken = CancellationToken.None;
        A.CallTo(() => fixture.Gateway.ListDatabasesAsync(A<MongoConnectionTarget>._, A<CancellationToken>._))
            .ReturnsLazily((MongoConnectionTarget _, CancellationToken token) =>
            {
                executionToken = token;
                return WaitForCancellationAsync(token);
            });
        var editor = new ConnectionEditor(fixture.Connections);

        var act = () => editor.TestAsync(new ConnectionDraft(), CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10));

        await act.Should().ThrowAsync<TimeoutException>().WithMessage("Connection test timed out after 3 seconds.");
        executionToken.IsCancellationRequested.Should().BeTrue();
        A.CallTo(() => fixture.Repository.AddAsync(A<StoredConnection>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task Should_PreserveDriverTimeoutDetails_WhenDriverFailsBeforeDeadline()
    {
        await using var fixture = new WorkspaceFixture();
        var exception = new TimeoutException("Server selection timed out.\n" + new string('x', 2000));
        A.CallTo(() => fixture.Gateway.ListDatabasesAsync(A<MongoConnectionTarget>._, A<CancellationToken>._))
            .Throws(exception);
        var editor = new ConnectionEditor(fixture.Connections);

        var failure = await Record.ExceptionAsync(() => editor.TestAsync(new ConnectionDraft(), CancellationToken.None));

        failure.Should().BeSameAs(exception);
    }

    private static async Task<IReadOnlyList<DatabaseInfo>> WaitForCancellationAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        return [];
    }
}
