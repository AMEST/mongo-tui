using Terminal.Gui.Configuration;
using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using TuiAttribute = Terminal.Gui.Drawing.Attribute;

namespace MongoTui.TUI.Themes;

/// <summary>Dark color scheme applied to the application shell.</summary>
public static class DarkTheme
{
    public const string Name = "MongoTuiDark";

    public static Scheme Create() => new()
    {
        Normal = new TuiAttribute(ColorName16.Gray, ColorName16.Black),
        HotNormal = new TuiAttribute(ColorName16.BrightYellow, ColorName16.Black),
        Focus = new TuiAttribute(ColorName16.Black, ColorName16.Cyan),
        HotFocus = new TuiAttribute(ColorName16.Black, ColorName16.BrightYellow),
        Editable = new TuiAttribute(ColorName16.White, ColorName16.Black),
        ReadOnly = new TuiAttribute(ColorName16.DarkGray, ColorName16.Black),
    };

    /// <summary>Registers the scheme so it can be referenced by name.</summary>
    public static void Register() => SchemeManager.AddScheme(Name, Create());

    /// <summary>Registers and applies the scheme to a view tree.</summary>
    public static void Apply(View view)
    {
        Register();
        view.SetScheme(Create());
    }
}
