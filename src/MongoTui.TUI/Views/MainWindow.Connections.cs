using MongoTui.Application.Localization;
using MongoTui.Application.Models;
using MongoTui.Domain.Connections;
using MongoTui.TUI.ViewModels;
using Microsoft.Extensions.Logging;
using MongoTui.Application.Diagnostics;

namespace MongoTui.TUI.Views;

public sealed partial class MainWindow
{
    private async Task ConnectAsync(ConnectionProfile profile, MongoConnectionTarget target)
    {
        Exception? failure = null;
        try
        {
            await OpenConnectionAsync(profile, target);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Connection opening cancelled");
            SetStatus(_localizer.Get(LocalizationKeys.Ssh.Cancelled));
        }
        catch (Exception exception)
        {
            _logger.LogWarning("Connection {ConnectionId} opening failed: {Failure}", profile.Id.Value, ExceptionDiagnostics.Describe(exception));
            SetStatus(exception.Message);
            failure = exception;
        }
        if (failure is not null && Application is { } application)
        {
            application.Run(new ErrorDialog(_localizer, application, _localizer.Get(LocalizationKeys.Common.Error), failure.Message));
        }
    }

    private async Task OpenConnectionAsync(ConnectionProfile profile, MongoConnectionTarget target)
    {
        var connection = await PrepareConnectionAsync(profile, target);
        try
        {
            var databases = await _gateway.ListDatabasesAsync(connection.Target, CancellationToken.None);
            await AddConnectedAsync(profile, connection, databases);
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    private async Task<TunnelConnection> PrepareConnectionAsync(ConnectionProfile profile, MongoConnectionTarget target)
    {
        var previous = _tree.Connections.FirstOrDefault(active => active.Profile.Id == profile.Id);
        if (profile.Ssh.Enabled && profile.Ssh.LocalPort > 0 && previous?.Tunnel?.LocalPort == profile.Ssh.LocalPort)
        {
            await DisconnectAsync(previous);
        }

        return await _tunnels.OpenAsync(profile, target, CancellationToken.None);
    }

    private async Task AddConnectedAsync(
        ConnectionProfile profile,
        TunnelConnection connection,
        IReadOnlyList<DatabaseInfo> databases)
    {
        var previous = _tree.Connections.FirstOrDefault(active => active.Profile.Id == profile.Id);
        if (previous is not null)
        {
            await DisconnectAsync(previous);
        }

        _tree.AddOrReplace(new ActiveConnection(profile, connection.Target,
            databases.OrderBy(database => database.Name, StringComparer.OrdinalIgnoreCase).ToList(), connection.Tunnel));
        RebuildTree();
        _logger.LogInformation("Connection {ConnectionId} opened", profile.Id.Value);
        SetStatus(_localizer.Format(LocalizationKeys.Status.Connected, profile.Name, databases.Count));
    }

    private async Task DisconnectAsync(ActiveConnection connection)
    {
        _tree.Remove(connection.Profile.Id);
        try
        {
            await _queryTabs.CloseForConnectionAsync(connection.Profile.Id);
        }
        finally
        {
            if (connection.Tunnel is not null)
            {
                await connection.Tunnel.DisposeAsync();
            }
        }

        RebuildTree();
        _logger.LogInformation("Connection {ConnectionId} closed", connection.Profile.Id.Value);
        SetStatus(_localizer.Format(LocalizationKeys.Status.Disconnected, connection.Profile.Name));
    }
}
