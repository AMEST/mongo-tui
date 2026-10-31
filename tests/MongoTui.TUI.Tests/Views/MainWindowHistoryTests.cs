using System.Reflection;
using AwesomeAssertions;
using FakeItEasy;
using MongoTui.Application.Models;
using MongoTui.Domain.Connections;
using MongoTui.Domain.Primitives;
using MongoTui.TUI.ViewModels;
using MongoTui.TUI.Views;

namespace MongoTui.TUI.Tests.Views;

public sealed class MainWindowHistoryTests
{
    [Theory]
    [InlineData("en", false)]
    [InlineData("ru", false)]
    [InlineData("en", true)]
    [InlineData("ru", true)]
    public async Task Should_OpenOriginalContextWithoutExecution_WhenHistoryEntryIsSelected(string language, bool connected)
    {
        await using var host = new TuiFeatureTestHost(language);
        var profile = ConnectionProfile.Create(ConnectionId.New(), "saved", "mongodb://localhost");
        var target = new MongoConnectionTarget(profile.Uri);
        var entry = new QueryHistoryEntry { ConnectionId = profile.Id, Database = "history_db", Collection = "orders",
            Script = "db.orders.deleteMany({})", RanAt = DateTimeOffset.UtcNow, Success = true };
        var databases = new TaskCompletionSource<IReadOnlyList<DatabaseInfo>>(TaskCreationOptions.RunContinuationsAsynchronously);
        A.CallTo(() => host.Connections.GetAsync(profile.Id, A<CancellationToken>._)).Returns(new StoredConnection(profile, null));
        A.CallTo(() => host.Gateway.ListDatabasesAsync(target, A<CancellationToken>._)).Returns(databases.Task);
        var tree = (TreeViewModel)typeof(MainWindow).GetField("_tree", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(host.Window)!;
        if (connected) tree.AddOrReplace(new ActiveConnection(profile, target, [new DatabaseInfo("other", false)]));
        host.Tabs.AddTab(ConnectionId.New(), target, "unrelated", script: "draft");
        var operation = Schedule(host, entry, profile, databases);

        host.App.Run(host.Window);

        await operation();
        host.Tabs.TabCount.Should().Be(2);
        host.Tabs.ActiveTab!.ConnectionId.Should().Be(profile.Id);
        host.Tabs.ActiveTab.ConnectionName.Should().Be("saved");
        host.Tabs.ActiveTab.Database.Should().Be("history_db");
        host.Tabs.ActiveTab.Collection.Should().Be("orders");
        host.Tabs.ActiveEditorView!.Text.Should().Be(entry.Script);
        A.CallTo(() => host.Session.EvaluateAsync(A<string>._, A<string?>._, A<CancellationToken>._)).MustNotHaveHappened();
        tree.Connections.Should().ContainSingle();
    }

    private static Func<Task> Schedule(TuiFeatureTestHost host, QueryHistoryEntry entry, ConnectionProfile profile,
        TaskCompletionSource<IReadOnlyList<DatabaseInfo>> databases)
    {
        Task? operation = null;
        host.App.AddTimeout(TimeSpan.FromMilliseconds(30), () =>
        {
            operation = (Task)typeof(MainWindow).GetMethod("OpenHistoryEntryAsync", BindingFlags.NonPublic | BindingFlags.Instance)!
                .Invoke(host.Window, [entry, new[] { profile }])!;
            _ = CompleteAsync(databases);
            return false;
        });
        host.App.AddTimeout(TimeSpan.FromMilliseconds(20), () =>
        {
            if (operation?.IsCompleted != true) return true;
            host.App.RequestStop(host.Window);
            return false;
        });
        host.App.AddTimeout(TimeSpan.FromSeconds(3), () => { host.App.RequestStop(host.Window); return false; });
        return () => operation!;
    }

    private static async Task CompleteAsync(TaskCompletionSource<IReadOnlyList<DatabaseInfo>> response)
    {
        await Task.Delay(80).ConfigureAwait(false);
        response.SetResult([new DatabaseInfo("other", false)]);
    }
}
