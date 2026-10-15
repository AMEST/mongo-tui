using System.Drawing;
using Terminal.Gui.Views;

#pragma warning disable CS0618 // The query workspace uses Terminal.Gui's TextView editor.

namespace MongoTui.TUI.Views;

/// <summary>Places the caret in a method's arguments after keyboard or mouse acceptance.</summary>
internal sealed class QueryAutocomplete(Func<Suggestion, int> caretBack) : TextViewAutocomplete
{
    protected override bool InsertSelection(Suggestion accepted)
    {
        var back = caretBack(accepted);
        if (!base.InsertSelection(accepted)) return false;
        if (back > 0 && HostControl is TextView editor)
            editor.InsertionPoint = new Point(editor.CurrentColumn - back, editor.CurrentRow);
        return true;
    }
}
