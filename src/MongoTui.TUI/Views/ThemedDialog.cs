using MongoTui.TUI.Themes;
using Terminal.Gui.Views;

namespace MongoTui.TUI.Views;

/// <summary>Base dialog that applies the application color scheme.</summary>
public class ThemedDialog : Dialog
{
    protected ThemedDialog() => DarkTheme.Apply(this);
}
