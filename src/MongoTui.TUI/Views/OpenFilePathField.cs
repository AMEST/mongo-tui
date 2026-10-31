using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace MongoTui.TUI.Views;

/// <summary>Starts a file path in the home directory and keeps it when typing a filename.</summary>
internal sealed class OpenFilePathField : TextField
{
    public OpenFilePathField()
    {
        SuggestedDirectory = DialogPathDefaults.OpenDirectory(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        Text = SuggestedDirectory;
    }

    public string SuggestedDirectory { get; }

    protected override void OnHasFocusChanged(bool newHasFocus, View? previousFocused, View? focused)
    {
        base.OnHasFocusChanged(newHasFocus, previousFocused, focused);
        if (!newHasFocus || Text != SuggestedDirectory) return;
        MoveEnd();
        ClearAllSelection();
        Used = true;
    }
}
