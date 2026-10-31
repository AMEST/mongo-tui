using MongoTui.Application.Localization;
using Terminal.Gui.Input;

namespace MongoTui.TUI.Views;

internal static class TextContextMenuLabels
{
    public static string? GetHelpKey(Command? command) => GetKey(command) is { } key ? key + ".Help" : null;

    public static string? GetKey(Command? command) => command switch
    {
        Command.SelectAll => LocalizationKeys.TextEditing.SelectAll,
        Command.DeleteAll => LocalizationKeys.TextEditing.Clear,
        Command.Copy => LocalizationKeys.TextEditing.Copy,
        Command.Cut => LocalizationKeys.TextEditing.Cut,
        Command.Paste => LocalizationKeys.TextEditing.Paste,
        Command.Undo => LocalizationKeys.TextEditing.Undo,
        Command.Redo => LocalizationKeys.TextEditing.Redo,
        _ => null,
    };
}
