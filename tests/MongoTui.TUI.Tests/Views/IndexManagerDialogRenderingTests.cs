using AwesomeAssertions;
using FakeItEasy;
using MongoTui.Application.Abstractions.Mongo;
using MongoTui.Application.Models;
using MongoTui.Application.Services;
using MongoTui.Domain.Collections;
using MongoTui.Domain.Primitives;
using MongoTui.TUI.Localization;
using MongoTui.TUI.Views;
using Terminal.Gui.App;
using Terminal.Gui.Drivers;
using Terminal.Gui.Input;
using Terminal.Gui.Views;

namespace MongoTui.TUI.Tests.Views;

public sealed class IndexManagerDialogRenderingTests
{
    private static readonly MongoConnectionTarget Target = new("mongodb://localhost");
    private static readonly CollectionRef Collection = new(DatabaseName.Create("app"), CollectionName.Create("items"));
    private static readonly IndexInfo Index = new() { Name = "email_date", Unique = true,
        Fields = [new IndexField("email", IndexDirection.Ascending), new IndexField("date", IndexDirection.Descending)] };

    [Theory]
    [InlineData("en", false, false)]
    [InlineData("ru", false, false)]
    [InlineData("en", true, false)]
    [InlineData("ru", true, false)]
    [InlineData("en", false, true)]
    [InlineData("ru", false, true)]
    public async Task Should_ShowDetailsAndConfirmDrop_WhenNestedIndexDialogLoads(string language, bool confirm, bool fails)
    {
        var localizer = new ResxLocalizer();
        localizer.SetLanguage(language);
        var gateway = A.Fake<IMongoGateway>();
        var response = new TaskCompletionSource<IReadOnlyList<IndexInfo>>(TaskCreationOptions.RunContinuationsAsynchronously);
        A.CallTo(() => gateway.ListIndexesAsync(Target, Collection, A<CancellationToken>._)).ReturnsNextFromSequence(
            response.Task, Task.FromResult<IReadOnlyList<IndexInfo>>([]));
        A.CallTo(() => gateway.GetCollectionStatsAsync(Target, Collection, A<CancellationToken>._)).Returns(new CollectionStats { Name = "items" });
        using IApplication app = Terminal.Gui.App.Application.Create();
        app.Init(DriverRegistry.Names.ANSI);
        using var parent = new Dialog();
        using var dialog = new IndexManagerDialog(new IndexService(gateway), localizer, app, Target, Collection);
        var state = Schedule(app, parent, dialog, response, confirm, fails);

        app.Run(parent);

        await state.Load();
        state.Rendered().Should().BeTrue();
        if (confirm && !fails) A.CallTo(() => gateway.DropIndexAsync(Target, Collection, Index.Name, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
        else A.CallTo(() => gateway.DropIndexAsync(Target, Collection, A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
        dialog.Buttons[0].Enabled.Should().BeTrue();
        dialog.Buttons[1].Enabled.Should().BeTrue();
    }

    private static (Func<Task> Load, Func<bool> Rendered) Schedule(IApplication app, Dialog parent,
        IndexManagerDialog dialog, TaskCompletionSource<IReadOnlyList<IndexInfo>> response, bool confirm, bool fails)
    {
        Task? load = null;
        var rendered = false;
        var dropping = false;
        app.AddTimeout(TimeSpan.Zero, () => { app.Run(dialog); app.RequestStop(parent); return false; });
        app.AddTimeout(TimeSpan.FromMilliseconds(30), () => { load = dialog.LoadAsync(); _ = CompleteAsync(response, fails); return false; });
        app.AddTimeout(TimeSpan.FromMilliseconds(20), () =>
        {
            if (app.TopRunnableView is not ConfirmDialog confirmation) return true;
            confirmation.Buttons[confirm ? 0 : 1].InvokeCommand(Command.Accept);
            return false;
        });
        app.AddTimeout(TimeSpan.FromMilliseconds(20), () =>
        {
            if (load?.IsCompleted != true) return true;
            if (app.TopRunnableView != dialog) return true;
            if (!rendered) rendered = ScreenText(app).Contains(fails ? "Unavailable" : "email: 1, date: -1", StringComparison.Ordinal);
            if (!rendered) return true;
            if (!fails && !dropping)
            {
                dropping = true;
                dialog.Buttons[2].InvokeCommand(Command.Accept);
                return true;
            }
            app.RequestStop(dialog);
            return false;
        });
        app.AddTimeout(TimeSpan.FromSeconds(3), () => { app.RequestStop(); app.RequestStop(dialog); return false; });
        return (() => load!, () => rendered);
    }

    private static async Task CompleteAsync(TaskCompletionSource<IReadOnlyList<IndexInfo>> response, bool fails)
    {
        await Task.Delay(80).ConfigureAwait(false);
        if (fails) response.SetException(new IOException("Unavailable"));
        else response.SetResult([Index]);
    }

    private static string ScreenText(IApplication app)
    {
        var screen = app.Driver!.Contents!;
        return string.Join("\n", Enumerable.Range(0, screen.GetLength(0)).Select(row =>
            string.Concat(Enumerable.Range(0, screen.GetLength(1)).Select(column => screen[row, column].Grapheme))));
    }
}
