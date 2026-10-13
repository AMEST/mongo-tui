using Terminal.Gui.Input;
using Terminal.Gui.Views;

#pragma warning disable CS0618 // TextView is superseded by the external Editor package; kept for M6.

namespace MongoTui.TUI.Views;

/// <summary>
/// Read-only result view that moves the caret to the row under a context-menu click.
/// This lets document actions operate on the clicked document instead of the stale caret.
/// </summary>
internal sealed class ResultTextView : TextView
{
    private int? _contextRow;

    /// <summary>Row the last context menu was opened on, or the caret when opened from the keyboard.</summary>
    public int DocumentRow => _contextRow ?? CurrentRow;

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
