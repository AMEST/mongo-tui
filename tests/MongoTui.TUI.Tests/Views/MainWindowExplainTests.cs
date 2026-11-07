using AwesomeAssertions;
using FakeItEasy;
using MongoDB.Bson;
using MongoTui.Application.Models;
using MongoTui.Domain.Diagnostics;
using MongoTui.Domain.Primitives;
using MongoTui.TUI.Views;
using Terminal.Gui.App;
using Terminal.Gui.Input;
using Terminal.Gui.Views;

namespace MongoTui.TUI.Tests.Views;

public sealed class MainWindowExplainTests
{
    [Theory]
    [InlineData("en", false)]
    [InlineData("ru", false)]
    [InlineData("en", true)]
    [InlineData("ru", true)]
    public async Task Should_DisplayPlanAndEditableQuery_WhenExplainShortcutIsPressed(string language, bool alternative)
    {
        await using var host = new TuiFeatureTestHost(language);
        const string script = "// write your query below this line\ndb.orders.find({}).limit(50).toArray()";
        var target = new MongoConnectionTarget("mongodb://localhost");
        host.Tabs.AddTab(ConnectionId.New(), target, "shop", "orders", script);
        var response = new TaskCompletionSource<BsonDocument>(TaskCreationOptions.RunContinuationsAsynchronously);
        A.CallTo(() => host.Diagnostics.ExplainAsync(target, A<ExplainRequest>._, A<CancellationToken>._)).Returns(response.Task);
        var rendered = false;
        host.App.AddTimeout(TimeSpan.FromMilliseconds(20), () =>
        {
            if (host.App.TopRunnableView is not ExplainDialog dialog || !ScreenText(host.App).Contains("IXSCAN", StringComparison.Ordinal)) return true;
#pragma warning disable CS0618
            dialog.SubViews.OfType<TextView>().Single(view => !view.ReadOnly).Text.Should().Be(script);
#pragma warning restore CS0618
            rendered = true; host.App.RequestStop(dialog); return false;
        });
        host.App.AddTimeout(TimeSpan.FromSeconds(3), () =>
        { if (host.App.TopRunnableView is ExplainDialog dialog) host.App.RequestStop(dialog); host.App.RequestStop(host.Window); return false; });
        host.App.AddTimeout(TimeSpan.Zero, () =>
        {
            _ = CompleteAsync(response);
            host.App.Keyboard.RaiseKeyDownEvent(alternative ? Key.Enter.WithCtrl.WithShift : Key.F8);
            host.App.RequestStop(host.Window); return false;
        });

        host.App.Run(host.Window);

        rendered.Should().BeTrue(); host.Tabs.ActiveTab!.Script.Should().Be(script);
        A.CallTo(() => host.Diagnostics.ExplainAsync(target, A<ExplainRequest>.That.Matches(request =>
            request.Collection.Collection.Value == "orders" && request.Limit == 50), A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    private static async Task CompleteAsync(TaskCompletionSource<BsonDocument> response)
    {
        await Task.Delay(80).ConfigureAwait(false);
        response.TrySetResult(BsonDocument.Parse("{queryPlanner:{winningPlan:{stage:'IXSCAN',indexName:'order_id'}}}"));
    }

    private static string ScreenText(IApplication app)
    {
        var cells = app.Driver!.Contents!;
        return string.Join('\n', Enumerable.Range(0, cells.GetLength(0)).Select(row =>
            string.Concat(Enumerable.Range(0, cells.GetLength(1)).Select(column => cells[row, column].Grapheme))));
    }
}
