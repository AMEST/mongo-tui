using System.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.Views;

#pragma warning disable CS0618 // TextView is superseded by the external Editor package; kept for M6.

namespace MongoTui.TUI.Views;

/// <summary>
/// Read-only result view that moves the caret to the row under a context-menu click.
/// This lets document actions operate on the clicked document instead of the stale caret.
/// Plain arrows scroll the viewport immediately, independently of line length.
/// </summary>
internal sealed class ResultTextView : TextView
{
    private int? _contextRow;

    /// <summary>Row the last context menu was opened on, or the caret when opened from the keyboard.</summary>
    public int DocumentRow => _contextRow ?? CurrentRow;

    protected override bool OnKeyDown(Key key)
    {
        if (key == Key.CursorUp) return ScrollBy(0, -1);
        if (key == Key.CursorDown) return ScrollBy(0, 1);
        if (key == Key.CursorLeft) return ScrollBy(-1, 0);
        if (key == Key.CursorRight) return ScrollBy(1, 0);
        return base.OnKeyDown(key);
    }

    private bool ScrollBy(int dx, int dy)
    {
        _contextRow = null;
        if (Lines == 0) return true;
        var viewport = Viewport;
        var content = GetContentSize();
        var position = new Point(
            Math.Clamp(viewport.X + dx, 0, Math.Max(0, content.Width - viewport.Width)),
            Math.Clamp(viewport.Y + dy, 0, Math.Max(0, content.Height - viewport.Height)));
        var row = Math.Clamp(CurrentRow + dy, position.Y,
            Math.Max(position.Y, Math.Min(Lines - 1, position.Y + viewport.Height - 1)));
        var column = Math.Clamp(Math.Max(position.X, CurrentColumn + dx), 0, GetLine(row).Count);
        IsSelecting = false;
        InsertionPoint = new Point(column, row);
        // Preserve the requested scroll position after TextView adjusts to its hidden caret.
        ScrollTo(position);
        SetNeedsDraw();
        return true;
    }

    protected override bool OnMouseEvent(Mouse mouse)
    {
        if (ContextMenu is { } menu && mouse.Flags == menu.MouseFlags && mouse.Position is { } position)
        {
            _contextRow = Viewport.Y + Math.Clamp(position.Y, 0, Math.Max(Viewport.Height - 1, 0));
        }
        else if (!mouse.Flags.HasFlag(MouseFlags.PositionReport))
        {
            _contextRow = null;
        }

        return base.OnMouseEvent(mouse);
    }
}
