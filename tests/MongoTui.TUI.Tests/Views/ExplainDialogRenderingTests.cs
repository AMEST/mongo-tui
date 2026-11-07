using AwesomeAssertions;
using FakeItEasy;
using MongoDB.Bson;
using MongoTui.Application.Abstractions.Mongo;
using MongoTui.Application.Abstractions.Persistence;
using MongoTui.Application.Models;
using MongoTui.Application.Services;
using MongoTui.Domain.Diagnostics;
using MongoTui.Domain.Exceptions;
using MongoTui.Domain.Primitives;
using MongoTui.TUI.Localization;
using MongoTui.TUI.Views;
using Terminal.Gui.App;
using Terminal.Gui.Drivers;
using Terminal.Gui.Views;

namespace MongoTui.TUI.Tests.Views;

public sealed class ExplainDialogRenderingTests
{
    [Theory]
    [InlineData("en", false)]
    [InlineData("ru", false)]
    [InlineData("en", true)]
    [InlineData("ru", true)]
    public void Should_UpdateNestedDialogWithoutInput_WhenBackgroundExplainCompletes(string language, bool failure)
    {
        var gateway = A.Fake<IMongoDiagnosticsGateway>(); var preferences = A.Fake<ISettingsStore>();
        var response = new TaskCompletionSource<BsonDocument>(TaskCreationOptions.RunContinuationsAsynchronously);
        A.CallTo(() => gateway.ExplainAsync(A<MongoConnectionTarget>._, A<ExplainRequest>._, A<CancellationToken>._)).Returns(response.Task);
        var service = new ExplainService(gateway, new SettingsService(preferences), new DiagnosticsSettingsService(preferences));
        var localizer = new ResxLocalizer(); localizer.SetLanguage(language);
        using IApplication app = Terminal.Gui.App.Application.Create(); app.Init(DriverRegistry.Names.ANSI);
        using var parent = new Dialog();
        using var dialog = new ExplainDialog(service, new QueryExecutionContext { Target = new("mongodb://localhost"),
            ConnectionId = ConnectionId.New(), TabId = Guid.NewGuid(), Database = "shop", Script = "// query\ndb.orders.find({}).toArray()" }, localizer, app);
        Task? load = null; var completed = false;
        app.AddTimeout(TimeSpan.FromMilliseconds(20), () =>
        {
            var expected = failure ? localizer.Get("Diagnostics.Error.Unauthorized") : "COLLSCAN";
            if (load?.IsCompleted != true || !ScreenText(app).Contains(expected, StringComparison.Ordinal)) return true;
            completed = true; app.RequestStop(dialog); return false;
        });
        app.AddTimeout(TimeSpan.FromSeconds(3), () => { app.RequestStop(dialog); app.RequestStop(parent); return false; });
        app.AddTimeout(TimeSpan.Zero, () => { load = dialog.LoadAsync(); _ = CompleteAsync(); app.Run(dialog); app.RequestStop(parent); return false; });

        app.Run(parent);

        completed.Should().BeTrue();
        dialog.Buttons.Single(button => button.Text == localizer.Get("Explain.Run")).Enabled.Should().BeTrue();
        dialog.Buttons.Single(button => button.Text == localizer.Get("Explain.CopyJson")).Enabled.Should().Be(!failure);
        if (failure) dialog.SubViews.OfType<Label>().Should().Contain(label => label.Text == localizer.Get("Diagnostics.Error.Unauthorized"));
        else dialog.SubViews.OfType<TreeView<ExplainNode>>().Should().ContainSingle();
        async Task CompleteAsync()
        {
            await Task.Delay(60).ConfigureAwait(false);
            if (failure) response.SetException(new DiagnosticsException("Diagnostics.Error.Unauthorized"));
            else response.SetResult(BsonDocument.Parse("{queryPlanner:{winningPlan:{stage:'COLLSCAN'}}}"));
        }
    }

    private static string ScreenText(IApplication app)
    {
        var cells = app.Driver!.Contents!;
        return string.Join('\n', Enumerable.Range(0, cells.GetLength(0)).Select(row =>
            string.Concat(Enumerable.Range(0, cells.GetLength(1)).Select(column => cells[row, column].Grapheme))));
    }
}
