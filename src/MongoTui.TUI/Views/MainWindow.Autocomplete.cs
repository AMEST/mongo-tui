using System.Drawing;
using Terminal.Gui.Drivers;
using Terminal.Gui.Input;

namespace MongoTui.TUI.Views;

public sealed partial class MainWindow
{
    private bool HandleCompletionKey(Key key)
    {
        if (key == new Key(KeyCode.Space | KeyCode.CtrlMask))
        {
            if (_queryTabs.ActiveEditorView is { HasFocus: true })
                Safe(CompleteActiveQueryAsync);
            else if (_treeList.HasFocus)
                OpenTreeContextMenu(new Point(2, 2));
            else return false;

            key.Handled = true;
            return true;
        }
        if (key == Key.Tab && _queryTabs.ActiveEditorView is { HasFocus: true, Autocomplete.Visible: true } editor
            && editor.Autocomplete.Suggestions.Count > 0)
        {
            key.Handled = editor.Autocomplete.ProcessKey(editor.Autocomplete.SelectionKey);
            return key.Handled;
        }
        return false;
    }

    private async Task CompleteActiveQueryAsync()
    {
        if (_queryTabs.ActiveEditorView is not { } editor)
            return;

        editor.SetFocus();
        var message = await _queryTabs.CompleteActiveAsync(CancellationToken.None);
        if (message is not null && editor.HasFocus)
            SetStatus(_localizer.Get(message));
    }
}
