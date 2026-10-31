using System.IO;
using MongoTui.Application.Localization;
using MongoTui.Application.Models;
using MongoTui.Domain.Primitives;

namespace MongoTui.TUI.Views;

public sealed partial class MainWindow
{
    private bool _openingQueryFile;

    private async Task OpenQueryFileAsync()
    {
        if (_openingQueryFile || Application is not { } application) return;
        _openingQueryFile = true;
        try
        {
            Task open = Task.CompletedTask;
            await application.InvokeAsync(() =>
            {
                if (_documentLifetime.IsCancellationRequested) return;
                using var dialog = new OpenQueryFileDialog(_queryFiles, _localizer, application);
                _ = dialog.LoadAsync();
                application.Run(dialog);
                if (dialog.SelectedPath is { } path) open = OpenQueryFileEntryAsync(path);
            }).ConfigureAwait(false);
            await open.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (_documentLifetime.IsCancellationRequested) { }
        catch (Exception exception)
        {
            if (_documentLifetime.IsCancellationRequested) return;
            await application.InvokeAsync(() =>
            {
                if (!_documentLifetime.IsCancellationRequested) SetStatus(exception.Message);
            }).ConfigureAwait(false);
        }
        finally { _openingQueryFile = false; }
    }

    private async Task OpenQueryFileEntryAsync(string path)
    {
        var file = await _queryFiles.OpenAsync(path, _documentLifetime.Token).ConfigureAwait(false);
        await Application!.InvokeAsync(() =>
        {
            if (!_documentLifetime.IsCancellationRequested) ApplyQueryFile(file);
        }).ConfigureAwait(false);
    }

    private async Task SaveActiveQueryFileAsync()
    {
        if (Application is not { } application) return;
        if (_queryTabs.ActiveTab is not { } tab)
        {
            SetStatus(_localizer.Get(LocalizationKeys.QueryFiles.NoTarget));
            return;
        }

        var script = tab.Script;
        var initial = tab.QueryFilePath
            ?? DialogPathDefaults.QueryFile(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), tab.Database);
        QueryFile? result = null;
        using var dialog = new SaveQueryFileDialog(_localizer, application, initial, async path =>
        {
            result = await _queryFiles.SaveAsync(path, script, _documentLifetime.Token).ConfigureAwait(false);
        });
        application.Run(dialog);
        if (dialog.Saved && result is { } file)
        {
            tab.QueryFilePath = file.Path;
            _queryTabs.SetTitle(tab, Path.GetFileName(file.Path));
            SetStatus(_localizer.Format(LocalizationKeys.QueryFiles.Saved, file.Path));
        }
    }

    private void ApplyQueryFile(QueryFile file)
    {
        var title = Path.GetFileName(file.Path);
        if (_queryTabs.ActiveTab is { } tab)
        {
            _queryTabs.SetScript(tab, file.Script);
            tab.QueryFilePath = file.Path;
            _queryTabs.SetTitle(tab, title);
            SetStatus(_localizer.Format(LocalizationKeys.QueryFiles.Opened, file.Path));
            return;
        }

        if (SelectedRow() is { Owner: { } connection } row)
        {
            var model = _queryTabs.AddTab(row.Profile.Id, connection.Target, row.Database, row.Collection,
                file.Script, row.Profile.Name);
            model.QueryFilePath = file.Path;
            _queryTabs.SetTitle(model, title);
            SetStatus(_localizer.Format(LocalizationKeys.QueryFiles.Opened, file.Path));
            return;
        }

        SetStatus(_localizer.Get(LocalizationKeys.QueryFiles.NoTarget));
    }
}
