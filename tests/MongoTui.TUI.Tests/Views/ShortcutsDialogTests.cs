using AwesomeAssertions;
using FakeItEasy;
using MongoTui.TUI.Localization;
using MongoTui.TUI.Views;
using System.Drawing;
using Terminal.Gui.App;
using Terminal.Gui.Input;
using Terminal.Gui.Views;

#pragma warning disable CS0618 // TextView is superseded by the external Editor package.

namespace MongoTui.TUI.Tests.Views;

public sealed class ShortcutsDialogTests
{
    [Theory]
    [InlineData("en", "Keyboard shortcuts", "Run query", "Copy selection", "Focus menu bar", "reserves Cmd+C")]
    [InlineData("ru", "Горячие клавиши", "Выполнить запрос", "Копировать выделение", "Фокус на меню", "перехватывает Cmd+C")]
    public void Should_ShowLocalizedShortcuts_WhenOpened(
        string language, string title, string run, string copy, string menuBar, string macNote)
    {
        var localizer = new ResxLocalizer();
        localizer.SetLanguage(language);

        using var dialog = new ShortcutsDialog(localizer, A.Fake<IApplication>());
        var text = dialog.SubViews.OfType<TextView>().Single().Text;

        dialog.Title.Should().Be(title);
        text.Should().Contain("F5 / Ctrl+R");
        text.Should().Contain("Ctrl+C");
        text.Should().Contain("Ctrl+V / Cmd+V");
        text.Should().Contain("F10");
        text.Should().Contain(run);
        text.Should().Contain(copy);
        text.Should().Contain(menuBar);
        text.Should().Contain(macNote);
    }

    [Fact]
    public void Should_ListEveryEntry_WhenOpened()
    {
        var localizer = new ResxLocalizer();
        localizer.SetLanguage("en");

        using var dialog = new ShortcutsDialog(localizer, A.Fake<IApplication>());
        var text = dialog.SubViews.OfType<TextView>().Single().Text!;

        text.Split('\n').Should().HaveCountGreaterThanOrEqualTo(13);
        text.Should().Contain("Ctrl+Q");
    }

    [Theory]
    [InlineData("en")]
    [InlineData("ru")]
    public void Should_KeepContentAndButtonVisible_WhenDialogIsLaidOut(string language)
    {
        var localizer = new ResxLocalizer();
        localizer.SetLanguage(language);
        using var dialog = new ShortcutsDialog(localizer, A.Fake<IApplication>());
        dialog.BeginInit();
        dialog.EndInit();
        var text = dialog.SubViews.OfType<TextView>().Single();

        dialog.Layout(new Size(80, 24));

        text.Frame.Height.Should().BeGreaterThan(0);
        text.Text!.Split('\n').Length.Should().BeLessThanOrEqualTo(text.Frame.Height);
        text.Frame.Bottom.Should().BeLessThanOrEqualTo(dialog.Viewport.Height);
        dialog.Buttons[0].Frame.Bottom.Should().BeLessThanOrEqualTo(dialog.Viewport.Height);
    }

    [Fact]
    public void Should_CloseDialog_WhenOkIsAccepted()
    {
        var application = A.Fake<IApplication>();
        using var dialog = new ShortcutsDialog(new ResxLocalizer(), application);

        dialog.Buttons[0].InvokeCommand(Command.Accept);

        A.CallTo(() => application.RequestStop(dialog)).MustHaveHappenedOnceExactly();
    }
}
