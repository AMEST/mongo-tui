using MongoTui.Application.Localization;
using MongoTui.Application.Abstractions.Localization;
using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace MongoTui.TUI.Views;

/// <summary>Modal dialog that shows an error message.</summary>
public sealed class ErrorDialog : ThemedDialog
{
    public ErrorDialog(ILocalizer localizer, IApplication application, string title, string message)
    {
        Title = title;
        Width = 72;
        Height = 18;

#pragma warning disable CS0618 // TextView is superseded by the external Editor package.
        var text = new TextView { Multiline = true, ReadOnly = true };
#pragma warning restore CS0618
        text.X = 0;
        text.Y = 0;
        text.Width = Dim.Fill();
        text.Height = Dim.Fill();
        text.Text = message;
        Add(text);

        var okButton = new Button { Text = localizer.Get(LocalizationKeys.Common.Ok), IsDefault = true };
        okButton.OnAccept(() => application.RequestStop(this));
        this.AddButtons(okButton);
    }
}
