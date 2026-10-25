using AwesomeAssertions;
using FakeItEasy;
using Microsoft.Extensions.Logging;
using MongoTui.Application.Abstractions.Mongo;
using MongoTui.Application.Models;
using MongoTui.Domain.Exceptions;
using MongoTui.Domain.Primitives;
using MongoTui.Domain.Queries;
using MongoTui.Testing;
using MongoTui.Web.Services;

namespace MongoTui.Web.Tests;

public sealed class ServiceLoggingTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Should_LogOutcomeWithoutCredentials_When_UserSaved(bool fails)
    {
        var sink = new LoggingTestSink();
        using var loggers = sink.CreateFactory();
        await using var fixture = new UserManagementFixture(loggers);
        await fixture.ConnectAsync();
        var draft = new UserDraft { Username = "private-username", Password = "private-password" };
        draft.SetDatabaseAccess("orders", MongoTui.Domain.Users.DatabaseAccessLevel.Read);
        if (fails)
            A.CallTo(() => fixture.Gateway.CreateUserAsync(A<MongoConnectionTarget>._, A<DatabaseName>._,
                A<string>._, A<string>._, A<IReadOnlyList<MongoTui.Domain.Users.MongoRole>>._, A<CancellationToken>._))
                .ThrowsAsync(new DomainException("private-username private-password mongodb://user:secret@host"));

        var error = await Record.ExceptionAsync(() => fixture.Users.SaveAsync(fixture.Selection, draft, CancellationToken.None));

        var entry = sink.Entries.Single(entry => entry.Category == typeof(WebUserManagement).FullName);
        entry.Properties["ConnectionId"].Should().Be(fixture.Selection.ConnectionId.Value);
        entry.Properties["Database"].Should().Be("admin");
        entry.Properties["Action"].Should().Be("create_user");
        entry.Level.Should().Be(fails ? LogLevel.Warning : LogLevel.Information);
        (error is not null).Should().Be(fails);
        sink.Text.Should().NotContain(draft.Username).And.NotContain(draft.Password).And.NotContain("mongodb://");
    }

    [Fact]
    public async Task Should_LogConnectionFailureOnce_When_OpeningFails()
    {
        var sink = new LoggingTestSink();
        using var loggers = sink.CreateFactory();
        await using var fixture = new WorkspaceFixture(loggers);
        A.CallTo(() => fixture.Gateway.ListDatabasesAsync(A<MongoConnectionTarget>._, A<CancellationToken>._))
            .ThrowsAsync(new TimeoutException("Connection unavailable."));

        var act = () => fixture.Catalog.ConnectAsync(fixture.Profile, CancellationToken.None);

        await act.Should().ThrowAsync<TimeoutException>();
        var entry = sink.Entries.Single(entry => entry.Level >= LogLevel.Warning);
        entry.Properties["ConnectionId"].Should().Be(fixture.Profile.Id.Value);
        entry.Message.Should().Contain(nameof(TimeoutException));
    }

    [Fact]
    public async Task Should_ProvideConnectionAndTabScope_When_BrowseQueryRuns()
    {
        var sink = new LoggingTestSink();
        using var loggers = sink.CreateFactory();
        await using var fixture = new WorkspaceFixture(loggers);
        await fixture.Catalog.ConnectAsync(fixture.Profile, CancellationToken.None);
        A.CallTo(() => fixture.Gateway.FindAsync(A<MongoConnectionTarget>._, A<MongoTui.Domain.Collections.CollectionRef>._,
            A<string>._, A<QueryExecutionOptions>._, A<CancellationToken>._)).Returns(QueryResult.Empty(QueryResultKind.Documents));
        var tab = fixture.Workspace.Open(fixture.Profile.Id, "demo", "users");

        await fixture.Workspace.ExecuteAsync(tab);

        var entry = sink.Entries.Single(entry => entry.Message.StartsWith("Query completed", StringComparison.Ordinal));
        entry.Properties["TabId"].Should().Be(tab.Id);
        entry.Properties["ConnectionId"].Should().Be(tab.ConnectionId.Value);
        entry.Properties["Database"].Should().Be("demo");
        loggers.CreateLogger("outside").LogInformation("After query");
        sink.Entries.Last().Properties.Should().NotContainKey("TabId");
    }
}
