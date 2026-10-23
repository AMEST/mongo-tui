using AwesomeAssertions;
using MongoTui.TUI.Themes;
using MongoTui.TUI.Views;
using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.Drivers;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

#pragma warning disable CS0618 // Modal editors still use Terminal.Gui's TextView.

namespace MongoTui.TUI.Tests.Views;

public sealed class ThemedDialogRenderingTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void Should_RenderDistinctEmptyInputBackground_WhenModalFieldUnfocused(bool multiline, bool readOnly)
    {
        using IApplication app = Terminal.Gui.App.Application.Create();
        app.Init(DriverRegistry.Names.ANSI);
        using var dialog = new InputDialog();
        var container = new View { X = 1, Y = 1, Width = Dim.Fill(1), Height = 5 };
        View input = multiline ? new TextView { Multiline = true } : new TextField { Secret = true, ReadOnly = readOnly };
        input.Width = 20;
        input.Height = multiline ? 3 : 1;
        container.Add(input);
        var button = new Button { Text = "OK", X = 1, Y = 7 };
        dialog.Add(container, button);
        app.Begin(dialog);
        button.SetFocus();

        app.LayoutAndDraw();

        var frame = input.FrameToScreen();
        for (var column = frame.Left; column < frame.Right; column++)
        {
            app.Driver!.Contents![frame.Top, column].Attribute!.Value.Background.Should().Be((Color)ColorName16.DarkGray);
        }
        dialog.GetScheme().Normal.Background.Should().Be((Color)ColorName16.Black);
    }

    [Fact]
    public void Should_KeepInputBackground_WhenOutsideModal()
    {
        using IApplication app = Terminal.Gui.App.Application.Create();
        app.Init(DriverRegistry.Names.ANSI);
        using var shell = new Runnable<bool>();
        DarkTheme.Apply(shell);
        var field = new TextField { X = 1, Y = 1, Width = 20 };
        var button = new Button { Text = "OK", X = 1, Y = 3 };
        shell.Add(field, button);
        app.Begin(shell);
        button.SetFocus();

        app.LayoutAndDraw();

        var frame = field.FrameToScreen();
        app.Driver!.Contents![frame.Top, frame.Left].Attribute!.Value.Background.Should().Be((Color)ColorName16.Black);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, true)]
    public void Should_RenderContrastingText_WhenModalFieldHasValue(bool multiline, bool focused, bool readOnly)
    {
        using IApplication app = Terminal.Gui.App.Application.Create();
        app.Init(DriverRegistry.Names.ANSI);
        using var dialog = new InputDialog();
        View input = multiline ? new TextView { Multiline = true } : new TextField { ReadOnly = readOnly };
        input.X = 1;
        input.Y = 1;
        input.Width = 20;
        input.Height = multiline ? 3 : 1;
        input.Text = "Visible text";
        var button = new Button { Text = "OK", X = 1, Y = 7 };
        dialog.Add(input, button);
        app.Begin(dialog);
        if (focused) input.SetFocus();
        else button.SetFocus();
        if (input is TextField field) field.ClearAllSelection();

        app.LayoutAndDraw();

        var frame = input.FrameToScreen();
        var cells = app.Driver!.Contents!;
        var colors = string.Join(", ", Enumerable.Range(frame.Left, 12).Select(x =>
            $"{cells[frame.Top, x].Grapheme}: {cells[frame.Top, x].Attribute}"));
        for (var column = frame.Left; column < frame.Left + 12; column++)
        {
            var attribute = cells[frame.Top, column].Attribute!.Value;
            attribute.Foreground.Should().Be((Color)ColorName16.White, colors);
            attribute.Foreground.Should().NotBe(attribute.Background);
        }
    }

    [Fact]
    public void Should_RenderDistinctSelection_WhenModalTextSelected()
    {
        using IApplication app = Terminal.Gui.App.Application.Create();
        app.Init(DriverRegistry.Names.ANSI);
        using var dialog = new InputDialog();
        var input = new TextField { X = 1, Y = 1, Width = 20, Text = "Visible text" };
        dialog.Add(input);
        app.Begin(dialog);
        input.SetFocus();
        input.SelectAll();

        app.LayoutAndDraw();

        var frame = input.FrameToScreen();
        var attribute = app.Driver!.Contents![frame.Top, frame.Left].Attribute!.Value;
        attribute.Background.Should().NotBe(input.GetScheme().Focus.Background);
        attribute.Foreground.Should().NotBe(attribute.Background);
    }

    private sealed class InputDialog : ThemedDialog
    {
        public InputDialog()
        {
            Width = 40;
            Height = 12;
        }
    }
}
