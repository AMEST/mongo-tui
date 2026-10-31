using System.Reflection;
using AwesomeAssertions;
using FakeItEasy;
using MongoTui.Application.Abstractions.Persistence;
using MongoTui.Application.Localization;
using MongoTui.Application.Models;
using MongoTui.Domain.Connections;
using MongoTui.Domain.Primitives;
using MongoTui.TUI.Localization;
using MongoTui.TUI.Views;
using Terminal.Gui.App;
using Terminal.Gui.Drivers;
using Terminal.Gui.Input;
using Terminal.Gui.Views;

namespace MongoTui.TUI.Tests.Views;

public sealed class HistoryDialogRenderingTests
{
    [Theory]
    [InlineData("en", false, false, false)]
    [InlineData("ru", false, false, false)]
    [InlineData("en", true, false, false)]
    [InlineData("ru", true, false, false)]
    [InlineData("en", false, true, false)]
    [InlineData("ru", false, true, false)]
    [InlineData("en", false, false, true)]
    [InlineData("ru", false, false, true)]
    public async Task Should_UpdateNestedHistoryWithoutInput_WhenBackgroundLoadCompletes(
        string language, bool fails, bool switchProfile, bool close)
    {
        var localizer = new ResxLocalizer();
        localizer.SetLanguage(language);
        var first = ConnectionProfile.Create(ConnectionId.New(), "first", "mongodb://localhost");
        var second = ConnectionProfile.Create(ConnectionId.New(), "second", "mongodb://other");
        var history = A.Fake<IQueryHistoryRepository>();
        var response = new TaskCompletionSource<IReadOnlyList<QueryHistoryEntry>>(TaskCreationOptions.RunContinuationsAsynchronously);
        A.CallTo(() => history.GetRecentAsync(first.Id, 100, A<CancellationToken>._)).Returns(response.Task);
        A.CallTo(() => history.GetRecentAsync(second.Id, 100, A<CancellationToken>._)).Returns(new[] { Entry(second.Id, "second") });
        using IApplication app = Terminal.Gui.App.Application.Create();
        app.Init(DriverRegistry.Names.ANSI);
        using var parent = new Dialog();
        using var dialog = new HistoryDialog(history, localizer, app, [first, second]);
        var state = Schedule(app, parent, dialog, response, first.Id, localizer, fails, switchProfile, close);

        app.Run(parent);

        await state.Load();
        if (!close)
        {
            state.Rendered().Should().BeTrue("the history should repaint automatically:\n{0}", ScreenText(app));
            dialog.Buttons[0].Enabled.Should().Be(switchProfile || !fails);
        }
    }

    private static (Func<Task> Load, Func<bool> Rendered) Schedule(IApplication app, Dialog parent,
        HistoryDialog dialog, TaskCompletionSource<IReadOnlyList<QueryHistoryEntry>> response, ConnectionId id,
        ResxLocalizer localizer, bool fails, bool switchProfile, bool close)
    {
        Task load = Task.CompletedTask;
        var rendered = false;
        app.AddTimeout(TimeSpan.Zero, () => { app.Run(dialog); app.RequestStop(parent); return false; });
        app.AddTimeout(TimeSpan.FromMilliseconds(30), () =>
        {
            load = dialog.LoadAsync();
            dialog.Buttons[0].Enabled.Should().BeFalse();
            if (switchProfile) Field<DropDownList>(dialog, "_connection").Value = "2. second";
            if (close) dialog.Buttons[2].InvokeCommand(Command.Accept);
            _ = CompleteAsync(response, id, fails);
            return false;
        });
        app.AddTimeout(TimeSpan.FromMilliseconds(20), () =>
        {
            if (!load.IsCompleted) return true;
            var expected = switchProfile ? "second.find" : fails
                ? localizer.Get(LocalizationKeys.History.LoadError) : "first.find";
            rendered = ScreenText(app).Contains(expected, StringComparison.Ordinal);
            if (!rendered) return true;
            app.RequestStop(dialog);
            return false;
        });
        app.AddTimeout(TimeSpan.FromSeconds(3), () => { app.RequestStop(dialog); return false; });
        return (() => load, () => rendered);
    }

    private static async Task CompleteAsync(TaskCompletionSource<IReadOnlyList<QueryHistoryEntry>> response,
        ConnectionId id, bool fails)
    {
        await Task.Delay(80).ConfigureAwait(false);
        if (fails) response.SetException(new IOException("Unavailable"));
        else response.SetResult([Entry(id, "first")]);
    }

    private static QueryHistoryEntry Entry(ConnectionId id, string name) => new()
    {
        ConnectionId = id, Database = "app", Collection = "orders", Script = $"db.{name}.find({{}})",
        RanAt = DateTimeOffset.UtcNow, Success = true, ElapsedMs = 42,
    };

    private static string ScreenText(IApplication app)
    {
        var screen = app.Driver!.Contents!;
        return string.Join("\n", Enumerable.Range(0, screen.GetLength(0)).Select(row =>
            string.Concat(Enumerable.Range(0, screen.GetLength(1)).Select(column => screen[row, column].Grapheme))));
    }

    private static T Field<T>(object instance, string name) =>
        (T)instance.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(instance)!;
}
