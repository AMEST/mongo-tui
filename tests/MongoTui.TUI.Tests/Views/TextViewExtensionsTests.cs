using AwesomeAssertions;
using MongoTui.TUI.Views;
using Terminal.Gui.App;
using Terminal.Gui.Drivers;
using Terminal.Gui.Views;

#pragma warning disable CS0618 // TextView is superseded by the external Editor package.

namespace MongoTui.TUI.Tests.Views;

public sealed class TextViewExtensionsTests
{
    [Fact]
    public void Should_RequestDraw_When_TextPasted()
    {
        using IApplication app = Terminal.Gui.App.Application.Create();
        app.Init(DriverRegistry.Names.ANSI);
        using Runnable<bool> runnable = new();

        var view = new TestTextView { Width = 40, Height = 5, Text = "initial" };
        runnable.Add(view);
        app.Begin(runnable);
        app.LayoutAndDraw();

        view.TrackContents();
        app.LayoutAndDraw();
        view.NeedsDraw.Should().BeFalse();

        view.PasteText("pasted");

        view.Text.Should().Contain("pasted");
        view.NeedsDraw.Should().BeTrue();
    }

    private sealed class TestTextView : TextView
    {
        public bool PasteText(string text) => OnPaste(text);
    }
}