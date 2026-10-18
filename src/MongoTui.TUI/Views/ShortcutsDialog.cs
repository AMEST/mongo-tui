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
        ("Shortcuts.Run", "F5 / Ctrl+R"),
        ("Shortcuts.NewTab", "Ctrl+T"),
        ("Shortcuts.CloseTab", "Ctrl+W"),
        ("Shortcuts.Connections", "Ctrl+O"),
        ("Shortcuts.Refresh", "F9"),
        ("Shortcuts.Autocomplete", "Ctrl+Space"),
        ("Shortcuts.History", "Ctrl+H"),
        ("Shortcuts.Ai", "F6"),
        ("Shortcuts.Translate", "F7"),
        ("Shortcuts.Export", "F2"),
        ("Shortcuts.Import", "F3"),
        ("Shortcuts.Indexes", "Ctrl+K"),
        ("Shortcuts.Insert", "Ctrl+I"),
        ("Shortcuts.Edit", "Ctrl+E"),
        ("Shortcuts.Delete", "Ctrl+D"),
        ("Shortcuts.MoveFocus", "Tab / Shift+Tab"),
        ("Shortcuts.MenuBar", "F10"),
        ("Shortcuts.InsertTab", "Ctrl+Tab"),
        ("Shortcuts.Copy", "Ctrl+C"),
        ("Shortcuts.Paste", "Ctrl+V / Cmd+V"),
        ("Shortcuts.Cut", "Ctrl+X"),
        ("Shortcuts.Unlock", "F8"),
        ("Shortcuts.ChangePassword", "Ctrl+P"),
        ("Shortcuts.Quit", "Ctrl+Q"),
    ];

    public ShortcutsDialog(ILocalizer localizer, IApplication application)
    {
        Title = localizer.Get("Shortcuts.Title");
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

        var okButton = new Button { Text = localizer.Get("Common.Ok"), IsDefault = true };
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

        lines.Add(localizer.Get("Shortcuts.MacNote"));

        return string.Join('\n', lines);
    }

    private static string Format(ILocalizer localizer, (string Key, string Shortcut) entry) =>
        $"{localizer.Get(entry.Key)} — {entry.Shortcut}";
}
