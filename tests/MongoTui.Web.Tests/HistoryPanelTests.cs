using AwesomeAssertions;
using FakeItEasy;
using Microsoft.Extensions.DependencyInjection;
using MongoTui.Application.Abstractions.Persistence;
using MongoTui.Application.Models;
using MongoTui.Domain.Primitives;
using MongoTui.Web.Components.Panels;

namespace MongoTui.Web.Tests;

public sealed class HistoryPanelTests
{
    [Fact]
    public async Task Should_KeepLatestSelection_WhenPreviousHistoryRequestFinishesLate()
    {
        await using var fixture = new WorkspaceFixture();
        var history = A.Fake<IQueryHistoryRepository>();
        using var services = Services(fixture, history);
        await using var renderer = new ComponentTestRenderer(services);
        await renderer.MountAsync<HistoryPanel>(new());
        var oldConnection = ConnectionId.New();
        var newConnection = ConnectionId.New();
        var response = new TaskCompletionSource<IReadOnlyList<QueryHistoryEntry>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var previousToken = CancellationToken.None;
        A.CallTo(() => history.GetRecentAsync(oldConnection, 100, A<CancellationToken>._)).ReturnsLazily(call =>
        {
            previousToken = call.GetArgument<CancellationToken>(2);
            return response.Task;
        });
        A.CallTo(() => history.GetRecentAsync(newConnection, 100, A<CancellationToken>._))
            .Returns(new[] { Entry(newConnection, "current query") });

        var pending = renderer.ChangeAsync(oldConnection.ToString());
        await renderer.ChangeAsync(newConnection.ToString());
        response.SetResult([Entry(oldConnection, "stale query")]);
        await pending;

        previousToken.IsCancellationRequested.Should().BeTrue();
        (await renderer.TextAsync()).Should().Contain("current query").And.NotContain("stale query");
    }

    [Fact]
    public async Task Should_NotStartHistoryRead_WhenClosedBeforeProfilesFinishLoading()
    {
        await using var fixture = new WorkspaceFixture();
        var history = A.Fake<IQueryHistoryRepository>();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var response = new TaskCompletionSource<IReadOnlyList<StoredConnection>>(TaskCreationOptions.RunContinuationsAsynchronously);
        A.CallTo(() => fixture.Repository.GetAllAsync(A<CancellationToken>._)).ReturnsLazily(() =>
        {
            started.SetResult();
            return response.Task;
        });
        using var services = Services(fixture, history);
        await using var renderer = new ComponentTestRenderer(services);
        var mounting = renderer.MountAsync<HistoryPanel>(new());
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await renderer.CloseAsync();
        response.SetResult([new(fixture.Profile, null)]);
        await mounting.WaitAsync(TimeSpan.FromSeconds(5));

        A.CallTo(() => history.GetRecentAsync(A<ConnectionId>._, A<int>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    private static QueryHistoryEntry Entry(ConnectionId connection, string script) => new()
    {
        Id = 1, ConnectionId = connection, Script = script, RanAt = DateTimeOffset.UtcNow, Success = true,
    };

    private static ServiceProvider Services(WorkspaceFixture fixture, IQueryHistoryRepository history)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(fixture.Catalog);
        services.AddSingleton(fixture.Text);
        services.AddSingleton(history);
        return services.BuildServiceProvider();
    }
}
