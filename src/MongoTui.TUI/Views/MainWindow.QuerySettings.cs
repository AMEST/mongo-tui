using MongoTui.Application.Localization;
namespace MongoTui.TUI.Views;

public sealed partial class MainWindow
{
    private async Task OpenQuerySettingsAsync()
    {
        if (Application is null) return;
        var dialog = new QuerySettingsDialog(_settings, _localizer, Application);
        await dialog.LoadAsync(CancellationToken.None);
        Application.Run(dialog);
        if (dialog.Saved) SetStatus(_localizer.Get(LocalizationKeys.Query.SettingsSaved));
    }
}
