using MongoTui.Application.Models;
using MongoTui.Domain.Connections;
using MongoTui.TUI.ViewModels;
using Serilog;

namespace MongoTui.TUI.Views;

public sealed partial class MainWindow
{
    private async Task ConnectAsync(ConnectionProfile profile, MongoConnectionTarget target)
    {
        TunnelConnection? connection = null;
        var connected = false;
        Exception? failure = null;
        try
        {
            connection = await PrepareConnectionAsync(profile, target);
            var databases = await _gateway.ListDatabasesAsync(connection.Target, CancellationToken.None);
            await AddConnectedAsync(profile, connection, databases);
            connected = true;
        }
        catch (OperationCanceledException)
        {
            SetStatus(_localizer.Get("Ssh.Cancelled"));
        }
        catch (Exception exception)
        {
            Log.Warning("Connection failed ({ExceptionType})", exception.GetType().Name);
            SetStatus(exception.Message);
            failure = exception;
        }
        finally
        {
            if (!connected && connection is not null)
            {
                await connection.DisposeAsync();
            }
        }

        if (failure is not null && Application is { } application)
        {
            application.Run(new ErrorDialog(_localizer, application, _localizer.Get("Common.Error"), failure.Message));
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
        Log.Information("Connected to {Profile}", profile.Name);
        SetStatus(_localizer.Format("Status.Connected", profile.Name, databases.Count));
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
        Log.Information("Disconnected from {Profile}", connection.Profile.Name);
        SetStatus(_localizer.Format("Status.Disconnected", connection.Profile.Name));
    }
}
