using System.Drawing;
using AwesomeAssertions;
using MongoTui.TUI.Views;
using Terminal.Gui.Drivers;
using Terminal.Gui.Input;
using Terminal.Gui.Views;

#pragma warning disable CS0618 // TextView is superseded by the external Editor package.

namespace MongoTui.TUI.Tests.Views;

public sealed class ResultTextViewTests
{
    [Fact]
    public void Should_KeepViewport_WhenOutputArrivesAfterScrollingUp()
    {
        using var app = Terminal.Gui.App.Application.Create();
        app.Init(DriverRegistry.Names.ANSI);
        using Runnable<bool> runnable = new();
        var view = new ResultTextView { Width = 40, Height = 10, ReadOnly = true };
        runnable.Add(view);
        app.Begin(runnable);
        app.LayoutAndDraw();
        var text = string.Join('\n', Enumerable.Range(0, 25));
        view.ShowOutput(text, reset: true);
        app.LayoutAndDraw();
        var bottom = view.Viewport.Y;
        view.NewKeyDownEvent(Key.CursorUp);
        var scrolled = view.Viewport.Y;

        view.ShowOutput(text + "\nnew line", reset: false);
        app.LayoutAndDraw();

        bottom.Should().BeGreaterThan(0);
        scrolled.Should().Be(bottom - 1);
        view.Viewport.Y.Should().Be(scrolled);
    }

    [Fact]
    public void Should_ReportClickedRow_When_ContextMenuOpened()
    {
        using var app = Terminal.Gui.App.Application.Create();
        app.Init(DriverRegistry.Names.ANSI);
        using Runnable<bool> runnable = new();

        var view = new ResultTextView { Width = 40, Height = 10, ReadOnly = true, Text = "0\n1\n2\n3\n4" };
        runnable.Add(view);
        app.Begin(runnable);
        app.LayoutAndDraw();

        view.NewMouseEvent(new Mouse { Flags = MouseFlags.RightButtonClicked, Position = new Point(0, 3) });

        view.DocumentRow.Should().Be(3);
    }

    [Fact]
    public void Should_ReportCaretRow_When_NoContextClick()
    {
        using var app = Terminal.Gui.App.Application.Create();
        app.Init(DriverRegistry.Names.ANSI);
        using Runnable<bool> runnable = new();

        var view = new ResultTextView { Width = 40, Height = 10, ReadOnly = true, Text = "0\n1\n2\n3\n4" };
        runnable.Add(view);
        app.Begin(runnable);
        app.LayoutAndDraw();

        view.DocumentRow.Should().Be(0);
    }
}
