using System.Drawing;
using AwesomeAssertions;
using MongoTui.TUI.Localization;
using MongoTui.TUI.Views;
using Terminal.Gui.App;
using Terminal.Gui.Drivers;
using Terminal.Gui.Input;
using Terminal.Gui.Views;

namespace MongoTui.TUI.Tests.Views;

public sealed class ContextMenuDialogTests
{
    [Theory]
    [InlineData("en", false, 0)]
    [InlineData("ru", false, 0)]
    [InlineData("en", false, 2)]
    [InlineData("ru", false, 2)]
    [InlineData("en", true, 0)]
    [InlineData("ru", true, 0)]
    [InlineData("en", true, 2)]
    [InlineData("ru", true, 2)]
    public void Should_ReturnHighlightedItem_WhenEnterOrSpacePressed(string language, bool space, int selection)
    {
        using IApplication app = Terminal.Gui.App.Application.Create();
        app.Init(DriverRegistry.Names.ANSI);
        using var parent = new Dialog();
        var localizer = new ResxLocalizer();
        localizer.SetLanguage(language);
        using var dialog = new ContextMenuDialog(localizer, app, ["First", "Second", "Third"], Point.Empty);
        var observe = Schedule(app, parent, dialog, space ? new Key(' ') : Key.Enter, selection);

        app.Run(parent);

        observe().TimedOut.Should().BeFalse();
        observe().ParentResumed.Should().BeTrue();
        dialog.SelectedIndex.Should().Be(selection);
    }

    [Theory]
    [InlineData("en")]
    [InlineData("ru")]
    public void ShouldNot_SelectItem_WhenEscapePressed(string language)
    {
        using IApplication app = Terminal.Gui.App.Application.Create();
        app.Init(DriverRegistry.Names.ANSI);
        using var parent = new Dialog();
        var localizer = new ResxLocalizer();
        localizer.SetLanguage(language);
        using var dialog = new ContextMenuDialog(localizer, app, ["First", "Second", "Third"], Point.Empty);
        var observe = Schedule(app, parent, dialog, Key.Esc, 1);

        app.Run(parent);

        observe().TimedOut.Should().BeFalse();
        observe().ParentResumed.Should().BeTrue();
        dialog.SelectedIndex.Should().Be(-1);
    }

    private static Func<(bool TimedOut, bool ParentResumed)> Schedule(
        IApplication app, Dialog parent, ContextMenuDialog dialog, Key key, int selection)
    {
        var timedOut = false;
        var parentResumed = false;
        app.AddTimeout(TimeSpan.Zero, () =>
        {
            app.Run(dialog);
            parentResumed = app.TopRunnableView == parent;
            app.RequestStop(parent);
            return false;
        });
        app.AddTimeout(TimeSpan.FromMilliseconds(30), () =>
        {
            for (var index = 0; index < selection; index++) app.Keyboard.RaiseKeyDownEvent(Key.CursorDown);
            app.Keyboard.RaiseKeyDownEvent(key);
            return false;
        });
        app.AddTimeout(TimeSpan.FromSeconds(2), () => { timedOut = true; app.RequestStop(dialog); return false; });
        return () => (timedOut, parentResumed);
    }
}
