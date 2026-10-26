using MongoTui.Application.Localization;

namespace MongoTui.TUI.Views;

public sealed partial class MainWindow
{
    private Task OpenQuerySettingsAsync()
    {
        if (Application is not { } application) return Task.CompletedTask;
        using var dialog = new QuerySettingsDialog(_settings, _localizer, application);
        application.Iteration += Load;
        try
        {
            application.Run(dialog);
            if (dialog.Saved) SetStatus(_localizer.Get(LocalizationKeys.Query.SettingsSaved));
        }
        finally { application.Iteration -= Load; }
        return Task.CompletedTask;

        void Load(object? sender, Terminal.Gui.App.EventArgs<Terminal.Gui.App.IApplication?> args)
        {
            application.Iteration -= Load;
            _ = dialog.LoadAsync(CancellationToken.None);
        }
    }
}
