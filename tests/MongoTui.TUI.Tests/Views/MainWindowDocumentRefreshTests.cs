using System.Reflection;
using AwesomeAssertions;
using FakeItEasy;
using MongoDB.Bson;
using MongoTui.Application.Models;
using MongoTui.Domain.Collections;
using MongoTui.Domain.Primitives;
using MongoTui.Domain.Queries;
using MongoTui.TUI.ViewModels;
using MongoTui.TUI.Views;
using Terminal.Gui.Input;
using Terminal.Gui.Views;

namespace MongoTui.TUI.Tests.Views;

public sealed class MainWindowDocumentRefreshTests
{
    private const string Read = "db.items.find({}).limit(50).toArray()";
    private static readonly MongoConnectionTarget Target = new("mongodb://localhost");
    private static readonly CollectionRef Collection = new(DatabaseName.Create("app"), CollectionName.Create("items"));

    [Theory]
    [InlineData("en", "insert")]
    [InlineData("ru", "insert")]
    [InlineData("en", "edit")]
    [InlineData("ru", "edit")]
    [InlineData("en", "delete")]
    [InlineData("ru", "delete")]
    public async Task Should_RefreshOriginalReadAfterMutation_WhenDocumentDialogCompletes(string language, string operation)
    {
        await using var host = new TuiFeatureTestHost(language);
        var mutation = new TaskCompletionSource<long>(TaskCreationOptions.RunContinuationsAsynchronously);
        Configure(host, mutation);
        var tab = host.Tabs.AddTab(ConnectionId.New(), Target, "app", "items", Read, "local");
        await tab.ExecuteAsync(CancellationToken.None);
        host.Tabs.SetResultViewMode(ResultViewMode.Table);
        tab.Script = "db.items.deleteMany({})";
        var state = Schedule(host, mutation, operation);

        host.App.Run(host.Window);

        state.Rendered().Should().BeTrue("the results should repaint automatically; task={0}, result={1}, screen:\n{2}",
            state.Operation().Status, host.Tabs.ActiveResultView!.Text, ScreenText(host));
        await state.Operation();
        tab.Script.Should().Be("db.items.deleteMany({})");
        A.CallTo(() => host.Session.EvaluateAsync(Read, "app", A<CancellationToken>._)).MustHaveHappenedTwiceExactly();
        A.CallTo(() => host.Session.EvaluateAsync("db.items.deleteMany({})", A<string?>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    private static void Configure(TuiFeatureTestHost host, TaskCompletionSource<long> mutation)
    {
        A.CallTo(() => host.Session.EvaluateAsync(Read, "app", A<CancellationToken>._)).ReturnsNextFromSequence(
            Task.FromResult(Result("before")), Task.FromResult(Result("refreshed")));
        A.CallTo(() => host.Gateway.FindAsync(Target, Collection, A<string>._, A<QueryExecutionOptions>._, A<CancellationToken>._))
            .Returns(Result("before"));
        A.CallTo(() => host.Gateway.InsertOneAsync(Target, Collection, A<string>._, A<CancellationToken>._)).Returns(mutation.Task);
        A.CallTo(() => host.Gateway.ReplaceOneAsync(Target, Collection, A<string>._, A<string>._, A<CancellationToken>._)).Returns(mutation.Task);
        A.CallTo(() => host.Gateway.DeleteOneAsync(Target, Collection, A<string>._, A<CancellationToken>._)).Returns(mutation.Task);
    }

    private static (Func<Task> Operation, Func<bool> Rendered) Schedule(TuiFeatureTestHost host,
        TaskCompletionSource<long> mutation, string operation)
    {
        Task pending = Task.CompletedTask;
        var started = false;
        var rendered = false;
        host.App.AddTimeout(TimeSpan.FromMilliseconds(30), () =>
        {
            host.Tabs.ActiveEditorView!.Text = "db.items.deleteMany({})";
            host.Tabs.ActiveResultView!.SetFocus();
            host.Tabs.ActiveResultView.NewKeyDownEvent(Key.CursorDown);
            host.Tabs.ActiveResultView.NewKeyDownEvent(Key.CursorDown);
            var method = operation == "insert" ? "InsertDocumentAsync" : "EditDocumentAsync";
            pending = (Task)typeof(MainWindow).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(host.Window, null)!;
            return false;
        });
        host.App.AddTimeout(TimeSpan.FromMilliseconds(20), () =>
        {
            if (started || host.App.TopRunnableView is not DocumentEditDialog dialog) return !started;
            started = true;
            if (operation == "insert") Field<FrameView>(dialog, "_documentFrame").SubViews.OfType<SyntaxTextView>().Single().Text = "{name:'new'}";
            dialog.Buttons[operation == "delete" ? 1 : 0].InvokeCommand(Command.Accept);
            _ = CompleteAsync(mutation);
            return false;
        });
        host.App.AddTimeout(TimeSpan.FromMilliseconds(20), () =>
        {
            if (!started || !pending.IsCompleted || host.App.TopRunnableView != host.Window) return true;
            rendered = ScreenText(host).Contains("refreshed", StringComparison.Ordinal);
            if (!rendered) return true;
            host.App.RequestStop(host.Window);
            return false;
        });
        host.App.AddTimeout(TimeSpan.FromSeconds(4), () => { host.App.RequestStop(); host.App.RequestStop(host.Window); return false; });
        return (() => pending, () => rendered);
    }

    private static async Task CompleteAsync(TaskCompletionSource<long> response)
    {
        await Task.Delay(80).ConfigureAwait(false);
        response.SetResult(1);
    }

    private static QueryResult Result(string value) => new() { Kind = QueryResultKind.Documents,
        Documents = [new BsonDocument { { "_id", 1 }, { "name", value } }] };

    private static T Field<T>(object instance, string name) =>
        (T)instance.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(instance)!;

    private static string ScreenText(TuiFeatureTestHost host)
    {
        var screen = host.App.Driver!.Contents!;
        return string.Join("\n", Enumerable.Range(0, screen.GetLength(0)).Select(row =>
            string.Concat(Enumerable.Range(0, screen.GetLength(1)).Select(column => screen[row, column].Grapheme))));
    }
}
