using AwesomeAssertions;
using MongoTui.TUI.Themes;
using MongoTui.TUI.Views;
using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.Drivers;
using Terminal.Gui.Views;
using Terminal.Gui.ViewBase;

namespace MongoTui.TUI.Tests.Views;

public sealed class SyntaxTextViewTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Should_RenderTokenColorsAndPreserveText_WhenEditorDraws(bool readOnly)
    {
        using IApplication app = Terminal.Gui.App.Application.Create();
        app.Init(DriverRegistry.Names.ANSI);
        using var window = new Runnable<bool>();
        var editor = new SyntaxTextView { Width = 65, Height = 10, Multiline = true, WordWrap = false,
            ReadOnly = readOnly, Text = "const field = \"значение\"; // comment\n42" };
        DarkTheme.Apply(editor);
        window.Add(editor);
        app.Begin(window);
        editor.SetFocus();

        app.LayoutAndDraw();

        var screen = app.Driver!.Contents!;
        Foreground(screen, "const").Should().Be(new Color(ColorName16.BrightBlue));
        Foreground(screen, "значение").Should().Be(new Color(ColorName16.BrightGreen));
        Foreground(screen, "comment").Should().Be(new Color(ColorName16.DarkGray));
        Foreground(screen, "42").Should().Be(new Color(ColorName16.BrightMagenta));
        editor.Text.Should().Be("const field = \"значение\"; // comment\n42");
    }

    [Fact]
    public void Should_RecolorWithoutInput_WhenTextIsReplaced()
    {
        using IApplication app = Terminal.Gui.App.Application.Create();
        app.Init(DriverRegistry.Names.ANSI);
        using var window = new Runnable<bool>();
        var editor = new SyntaxTextView { Width = 30, Height = 5, Text = "\"123\"" };
        window.Add(editor);
        app.Begin(window);
        app.LayoutAndDraw();

        editor.Text = "123";
        app.LayoutAndDraw();

        Foreground(app.Driver!.Contents!, "123").Should().Be(new Color(ColorName16.BrightMagenta));
    }

    [Fact]
    public void Should_KeepSelectionColors_WhenHighlightedTextIsSelected()
    {
        using IApplication app = Terminal.Gui.App.Application.Create();
        app.Init(DriverRegistry.Names.ANSI);
        using var window = new Runnable<bool>();
        var editor = new SyntaxTextView { Width = 40, Height = 5, Text = "const field = \"value\"" };
        DarkTheme.Apply(editor);
        window.Add(editor);
        app.Begin(window);
        editor.SetFocus();

        editor.SelectAll();
        app.LayoutAndDraw();

        Foreground(app.Driver!.Contents!, "const").Should().Be(editor.GetAttributeForRole(VisualRole.Focus).Foreground);
        Foreground(app.Driver.Contents!, "value").Should().Be(editor.GetAttributeForRole(VisualRole.Focus).Foreground);
    }

    [Fact]
    public void Should_ColorFollowingNumber_WhenUnicodeGraphemesPrecedeIt()
    {
        using IApplication app = Terminal.Gui.App.Application.Create();
        app.Init(DriverRegistry.Names.ANSI);
        using var window = new Runnable<bool>();
        var editor = new SyntaxTextView { Width = 50, Height = 5, Text = "const é = \"😀 中\"; 42" };
        window.Add(editor);
        app.Begin(window);

        app.LayoutAndDraw();

        Foreground(app.Driver!.Contents!, "42").Should().Be(new Color(ColorName16.BrightMagenta));
    }

    [Fact]
    public void Should_KeepTokenColors_WhenEditorScrollsHorizontally()
    {
        using IApplication app = Terminal.Gui.App.Application.Create();
        app.Init(DriverRegistry.Names.ANSI);
        using var window = new Runnable<bool>();
        var editor = new SyntaxTextView { Width = 22, Height = 5, WordWrap = false,
            Text = "prefixprefixprefixprefix = \"needle\"; 42" };
        window.Add(editor);
        app.Begin(window);
        app.LayoutAndDraw();

        editor.ScrollTo(new System.Drawing.Point(20, 0));
        app.LayoutAndDraw();

        Foreground(app.Driver!.Contents!, "needle").Should().Be(new Color(ColorName16.BrightGreen));
        Foreground(app.Driver.Contents!, "42").Should().Be(new Color(ColorName16.BrightMagenta));
    }

    private static Color? Foreground(Cell[,] screen, string text)
    {
        for (var row = 0; row < screen.GetLength(0); row++)
        {
            for (var column = 0; column < screen.GetLength(1); column++)
            {
                var tail = string.Concat(Enumerable.Range(column, screen.GetLength(1) - column).Select(x => screen[row, x].Grapheme));
                if (tail.StartsWith(text, StringComparison.Ordinal)) return screen[row, column].Attribute?.Foreground;
            }
        }
        throw new InvalidOperationException($"Expected text '{text}' was not rendered.");
    }
}
