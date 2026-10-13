using Terminal.Gui.ViewBase;

namespace MongoTui.TUI.Views;

/// <summary>
/// Resolves the next focus target when cycling between the tree, query editor and result
/// panes. The query panes live inside a <see cref="Terminal.Gui.Views.Tabs"/> control whose
/// focus chain traps the caret, so navigation is resolved explicitly instead of through
/// <c>Command.NextTabStop</c>.
/// </summary>
internal static class PaneFocusCycle
{
    /// <summary>
    /// Returns the pane to focus when navigating from <paramref name="focused"/>, or
    /// <see langword="null"/> when the key press should be left to Terminal.Gui.
    /// </summary>
    public static View? Resolve(View? focused, IReadOnlyList<View> panes, View queryArea, bool forward)
    {
        if (focused is null || panes.Count < 2)
        {
            return null;
        }

        var current = IndexOf(focused, panes);

        if (current < 0)
        {
            if (!IsWithin(focused, queryArea) && !IsAdornment(focused))
            {
                return null;
            }

            current = panes.Count - 1;
        }

        var next = forward ? (current + 1) % panes.Count : (current - 1 + panes.Count) % panes.Count;
        return panes[next];
    }

    private static int IndexOf(View focused, IReadOnlyList<View> panes)
    {
        for (var i = 0; i < panes.Count; i++)
        {
            if (IsWithin(focused, panes[i]))
            {
                return i;
            }
        }

        return -1;
    }

    private static bool IsWithin(View focused, View ancestor)
    {
        for (View? view = focused; view is not null; view = view.SuperView)
        {
            if (ReferenceEquals(view, ancestor))
            {
                return true;
            }
        }

        return false;
    }

    // The tab header (BorderView/TitleView of a Tabs page) is detached from the visual tree,
    // so it cannot be reached by walking SuperView up to the query area.
    private static bool IsAdornment(View focused)
    {
        for (View? view = focused; view is not null; view = view.SuperView)
        {
            if (view is AdornmentView)
            {
                return true;
            }
        }

        return false;
    }
}
