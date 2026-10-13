using MongoTui.Application.Abstractions.Localization;
using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace MongoTui.TUI.Views;

/// <summary>Modal yes/no confirmation dialog.</summary>
public sealed class ConfirmDialog : ThemedDialog
{
    public ConfirmDialog(ILocalizer localizer, IApplication application, string title, string message)
    {
        Title = title;
        Width = 64;
        Height = 9;

        Add(new Label { Text = message, X = 1, Y = 1, Width = Dim.Fill() });

        var yesButton = new Button { Text = localizer.Get("Common.Yes"), IsDefault = true };
        yesButton.OnAccept(() =>
        {
            Confirmed = true;
            application.RequestStop(this);
        });

        var noButton = new Button { Text = localizer.Get("Common.No") };
        noButton.OnAccept(() => application.RequestStop(this));

        AddButton(yesButton);
        AddButton(noButton);
    }

    public bool Confirmed { get; private set; }
}
