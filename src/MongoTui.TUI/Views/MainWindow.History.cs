using MongoTui.Application.Localization;
using MongoTui.Application.Models;
using MongoTui.Domain.Connections;
using MongoTui.TUI.ViewModels;

namespace MongoTui.TUI.Views;

public sealed partial class MainWindow
{
    private bool _openingHistory;

    private async Task OpenHistoryAsync()
    {
        if (_openingHistory || Application is not { } application) return;
        _openingHistory = true;
        var selected = _queryTabs.ActiveTab?.ConnectionId ?? SelectedRow()?.Profile.Id;
        try
        {
            var profiles = await _connections.GetProfilesAsync(_documentLifetime.Token).ConfigureAwait(false);
            if (_documentLifetime.IsCancellationRequested) return;
            Task open = Task.CompletedTask;
            await application.InvokeAsync(() =>
            {
                if (_documentLifetime.IsCancellationRequested) return;
                using var dialog = new HistoryDialog(_history, _localizer, application, profiles, selected);
                _ = dialog.LoadAsync();
                application.Run(dialog);
                if (dialog.SelectedEntry is { } entry) open = OpenHistoryEntryAsync(entry, profiles);
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
        finally { _openingHistory = false; }
    }

    private async Task OpenHistoryEntryAsync(QueryHistoryEntry entry, IReadOnlyList<ConnectionProfile> profiles)
    {
        var profile = profiles.First(item => item.Id == entry.ConnectionId);
        if (_tree.Connections.FirstOrDefault(item => item.Profile.Id == entry.ConnectionId) is { } active)
        {
            OpenHistoryTab(entry, active);
            return;
        }
        SetStatus(_localizer.Get(LocalizationKeys.Common.Working));
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_documentLifetime.Token);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        var target = await _connections.ResolveTargetAsync(profile.Id, timeout.Token).ConfigureAwait(false);
        var connection = await _tunnels.OpenAsync(profile, target, timeout.Token).ConfigureAwait(false);
        var adopted = false;
        try
        {
            var databases = await _gateway.ListDatabasesAsync(connection.Target, timeout.Token).ConfigureAwait(false);
            if (timeout.IsCancellationRequested) return;
            await Application!.InvokeAsync(() =>
            {
                if (timeout.IsCancellationRequested) return;
                var existing = _tree.Connections.FirstOrDefault(item => item.Profile.Id == profile.Id);
                var opened = existing ?? new ActiveConnection(profile, connection.Target,
                    databases.OrderBy(database => database.Name, StringComparer.OrdinalIgnoreCase).ToList(), connection.Tunnel);
                if (existing is null) { _tree.AddOrReplace(opened); adopted = true; RebuildTree(); }
                OpenHistoryTab(entry, opened);
            }).ConfigureAwait(false);
        }
        finally { if (!adopted) await connection.DisposeAsync().ConfigureAwait(false); }
    }

    private void OpenHistoryTab(QueryHistoryEntry entry, ActiveConnection connection)
    {
        _queryTabs.AddTab(entry.ConnectionId, connection.Target, entry.Database, entry.Collection,
            entry.Script, connection.Profile.Name);
        SetStatus(connection.Profile.Name);
    }
}
