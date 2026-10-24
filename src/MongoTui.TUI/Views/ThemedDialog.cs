using MongoTui.TUI.Themes;
using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using TuiAttribute = Terminal.Gui.Drawing.Attribute;

namespace MongoTui.TUI.Views;

/// <summary>Base dialog that applies the application color scheme.</summary>
public class ThemedDialog : Dialog
{
    protected ThemedDialog()
    {
        DarkTheme.Apply(this);
        Initialized += (_, _) => ApplyInputScheme(this);
    }

    private void ApplyInputScheme(View view)
    {
#pragma warning disable CS0618 // Modal editors still use Terminal.Gui's TextView.
        if (view is TextField or TextView { ReadOnly: false })
#pragma warning restore CS0618
        {
            var normal = new TuiAttribute(ColorName16.White, ColorName16.DarkGray);
            view.SetScheme(new Scheme(view.GetScheme())
            {
                Normal = normal,
                HotNormal = normal,
                Focus = new TuiAttribute(ColorName16.White, ColorName16.Blue),
                HotFocus = new TuiAttribute(ColorName16.BrightYellow, ColorName16.Blue),
                Active = new TuiAttribute(ColorName16.Black, ColorName16.Cyan),
                Editable = normal,
                ReadOnly = normal,
            });
        }

        foreach (var child in view.SubViews)
        {
            ApplyInputScheme(child);
        }
    }
}
