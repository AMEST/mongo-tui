using Terminal.Gui.Views;

#pragma warning disable CS0618 // TextView is superseded by the external Editor package; kept for M6.

namespace MongoTui.TUI.Views;

/// <summary>
/// Works around a Terminal.Gui 2.5 <see cref="TextView"/> defect: pasting runs through
/// <c>InsertAllText</c>, which raises <see cref="TextView.ContentsChanged"/> but never invalidates the
/// view. Typing works because <c>InsertText</c> calls <c>SetNeedsDraw</c>. Without this the pasted
/// text only appears after the next unrelated redraw (a cursor move or a click).
/// </summary>
internal static class TextViewExtensions
{
    /// <summary>Repaints <paramref name="view"/> whenever its contents change, and runs <paramref name="onChanged"/>.</summary>
    public static void TrackContents(this TextView view, Action? onChanged = null)
    {
        view.ContentsChanged += (_, _) =>
        {
            onChanged?.Invoke();
            view.SetNeedsDraw();
        };
    }
}
