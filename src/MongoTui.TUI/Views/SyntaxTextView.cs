using MongoTui.TUI.Editor;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.Views;
using TuiAttribute = Terminal.Gui.Drawing.Attribute;

#pragma warning disable CS0618 // Syntax coloring extends the TextView used by existing editors.

namespace MongoTui.TUI.Views;

/// <summary>Colors JavaScript/JSON tokens while preserving TextView editing and selection.</summary>
internal class SyntaxTextView : TextView
{
    private readonly Dictionary<List<Cell>, SyntaxTokenKind[]> _tokens = [];

    public SyntaxTextView()
    {
        KeyBindings.Add(Key.CursorLeft.WithAlt, Command.WordLeft);
        KeyBindings.Add(Key.CursorRight.WithAlt, Command.WordRight);
        KeyBindings.Add(Key.CursorLeft.WithAlt.WithShift, Command.WordLeftExtend);
        KeyBindings.Add(Key.CursorRight.WithAlt.WithShift, Command.WordRightExtend);
        KeyBindings.Add(Key.B.WithAlt, Command.WordLeft);
        KeyBindings.Add(Key.F.WithAlt, Command.WordRight);
        ContentsChanged += (_, _) => { _tokens.Clear(); SetNeedsDraw(); };
        DrawingContent += (_, _) => _tokens.Clear();
    }

    protected override void OnDrawNormalColor(List<Cell> line, int idxCol, int idxRow)
    {
        base.OnDrawNormalColor(line, idxCol, idxRow);
        Highlight(line, idxCol);
    }

    protected override void OnDrawReadOnlyColor(List<Cell> line, int idxCol, int idxRow)
    {
        base.OnDrawReadOnlyColor(line, idxCol, idxRow);
        Highlight(line, idxCol);
    }

    private void Highlight(List<Cell> line, int column)
    {
        if (!_tokens.TryGetValue(line, out var kinds))
        {
            kinds = TokenKinds(line);
            _tokens[line] = kinds;
        }
        if (column < 0 || column >= kinds.Length) return;
        Color? foreground = kinds[column] switch
        {
            SyntaxTokenKind.Keyword => new Color(ColorName16.BrightBlue),
            SyntaxTokenKind.String => new Color(ColorName16.BrightGreen),
            SyntaxTokenKind.Number => new Color(ColorName16.BrightMagenta),
            SyntaxTokenKind.Comment => new Color(ColorName16.DarkGray),
            SyntaxTokenKind.Operator => new Color(ColorName16.BrightYellow),
            _ => null,
        };
        if (foreground is { } color)
            SetAttribute(new TuiAttribute(color, GetAttributeForRole(VisualRole.Editable).Background));
    }

    private static SyntaxTokenKind[] TokenKinds(List<Cell> line)
    {
        var tokens = SyntaxHighlighter.Tokenize(Cell.ToString(line));
        var result = new SyntaxTokenKind[line.Count];
        if (tokens.Count == 0) return result;
        var offset = 0;
        var token = 0;
        var end = tokens[0].Text.Length;
        for (var column = 0; column < line.Count; column++)
        {
            while (offset >= end && token + 1 < tokens.Count) end += tokens[++token].Text.Length;
            result[column] = tokens[token].Kind;
            offset += line[column].Grapheme.Length;
        }
        return result;
    }
}
