using MongoTui.Application.Localization;
using MongoTui.Application.Abstractions.Localization;
using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

#pragma warning disable CS0618 // TextView is superseded by the external Editor package.

namespace MongoTui.TUI.Views;

/// <summary>Modal dialog listing the keyboard shortcuts and where they apply.</summary>
public sealed class ShortcutsDialog : ThemedDialog
{
    private const int ColumnWidth = 38;

    private static readonly (string Key, string Shortcut)[] Entries =
    [
        (LocalizationKeys.Shortcuts.Run, "F5 / Ctrl+R"),
        (LocalizationKeys.Shortcuts.NewTab, "Ctrl+T"),
        (LocalizationKeys.Shortcuts.CloseTab, "Ctrl+W"),
        (LocalizationKeys.Shortcuts.Connections, "Ctrl+O"),
        (LocalizationKeys.Shortcuts.Refresh, "F9"),
        (LocalizationKeys.Shortcuts.Autocomplete, "Ctrl+Space"),
        (LocalizationKeys.Shortcuts.History, "Ctrl+H"),
        (LocalizationKeys.Shortcuts.Ai, "F6"),
        (LocalizationKeys.Shortcuts.Translate, "F7"),
        (LocalizationKeys.Shortcuts.Export, "F2"),
        (LocalizationKeys.Shortcuts.Import, "F3"),
        (LocalizationKeys.Shortcuts.Indexes, "Ctrl+K"),
        (LocalizationKeys.Shortcuts.Insert, "Ctrl+I"),
        (LocalizationKeys.Shortcuts.Edit, "Ctrl+E"),
        (LocalizationKeys.Shortcuts.Delete, "Ctrl+D"),
        (LocalizationKeys.Shortcuts.MoveFocus, "Tab / Shift+Tab"),
        (LocalizationKeys.Shortcuts.MenuBar, "F10"),
        (LocalizationKeys.Shortcuts.InsertTab, "Ctrl+Tab"),
        (LocalizationKeys.Shortcuts.Copy, "Ctrl+C"),
        (LocalizationKeys.Shortcuts.Paste, "Ctrl+V / Cmd+V"),
        (LocalizationKeys.Shortcuts.Cut, "Ctrl+X"),
        (LocalizationKeys.Shortcuts.Unlock, "F8"),
        (LocalizationKeys.Shortcuts.ChangePassword, "Ctrl+P"),
        (LocalizationKeys.Shortcuts.Quit, "Ctrl+Q"),
    ];

    public ShortcutsDialog(ILocalizer localizer, IApplication application)
    {
        Title = localizer.Get(LocalizationKeys.Shortcuts.Title);
        Width = 80;
        Height = Dim.Percent(90);

        var text = new TextView
        {
            X = 1,
            Y = 1,
            Width = Dim.Fill(2),
            Height = Dim.Fill(),
            Multiline = true,
            ReadOnly = true,
            WordWrap = false,
            Text = BuildText(localizer),
        };
        Add(text);

        var okButton = new Button { Text = localizer.Get(LocalizationKeys.Common.Ok), IsDefault = true };
        okButton.OnAccept(() => application.RequestStop(this));
        this.AddButtons(okButton);
    }

    private static string BuildText(ILocalizer localizer)
    {
        var half = (Entries.Length + 1) / 2;
        var lines = new List<string>(half + 1);

        for (var row = 0; row < half; row++)
        {
            var left = Format(localizer, Entries[row]);
            var right = row + half < Entries.Length ? Format(localizer, Entries[row + half]) : string.Empty;
            lines.Add(right.Length == 0 ? left : left.PadRight(ColumnWidth) + right);
        }

        lines.Add(localizer.Get(LocalizationKeys.Shortcuts.MacNote));

        return string.Join('\n', lines);
    }

    private static string Format(ILocalizer localizer, (string Key, string Shortcut) entry) =>
        $"{localizer.Get(entry.Key)} — {entry.Shortcut}";
}
