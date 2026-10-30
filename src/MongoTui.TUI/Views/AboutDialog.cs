using MongoTui.Application.Localization;
using MongoTui.Application.Abstractions.Localization;
using MongoTui.TUI.Bootstrap;
using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace MongoTui.TUI.Views;

/// <summary>Modal dialog with application information and the entry assembly version.</summary>
public sealed class AboutDialog : ThemedDialog
{
    public AboutDialog(ILocalizer localizer, IApplication application)
    {
        Title = localizer.Get(LocalizationKeys.About.Title);
        Width = 64;
        Height = 16;

        Add(new Label { Text = localizer.Get(LocalizationKeys.App.Title), X = 1, Y = 1, Width = Dim.Fill(1) });
        Add(new Label { Text = localizer.Get(LocalizationKeys.About.Description), X = 1, Y = 3, Width = Dim.Fill(1) });
        Add(new Label
        {
            Text = localizer.Format(LocalizationKeys.About.Version, ApplicationVersion.GetInformationalVersion()),
            X = 1,
            Y = 5,
            Width = Dim.Fill(1),
        });
        Add(new Label { Text = localizer.Get(LocalizationKeys.About.License), X = 1, Y = 7, Width = Dim.Fill(1) });
        Add(new Label { Text = localizer.Get(LocalizationKeys.About.Copyright), X = 1, Y = 8, Width = Dim.Fill(1) });
        Add(new Label { Text = localizer.Get(LocalizationKeys.About.CoAuthor), X = 1, Y = 9, Width = Dim.Fill(1) });

        var okButton = new Button { Text = localizer.Get(LocalizationKeys.Common.Ok), IsDefault = true };
        okButton.OnAccept(() => application.RequestStop(this));
        this.AddButtons(okButton);
    }
}
