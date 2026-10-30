using MongoTui.Application.Abstractions.Mongo;
using Microsoft.Extensions.Logging;
using MongoTui.Application.Diagnostics;
using MongoTui.Application.Models;
using MongoTui.Application.Services;
using MongoTui.Domain.Connections;
using MongoTui.Domain.Primitives;

namespace MongoTui.Web.Services;

public sealed class ConnectionCatalog(
    ConnectionService profiles, ConnectionTunnelService tunnels, IMongoGateway gateway, ILogger<ConnectionCatalog> logger) : IAsyncDisposable
{
    private readonly CancellationTokenSource _lifetime = new();
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly List<WebConnection> _active = [];
    public IReadOnlyList<ConnectionProfile> Profiles { get; private set; } = [];
    public IReadOnlyList<WebConnection> Active => _active;
    public IEnumerable<ConnectionProfile> ExplorerProfiles => Profiles.Concat(_active.Select(connection => connection.Profile))
        .DistinctBy(profile => profile.Id)
        .OrderByDescending(profile => _active.Any(connection => connection.Profile.Id == profile.Id))
        .ThenBy(profile => profile.Name, StringComparer.OrdinalIgnoreCase);
    public event Action? Changed;

    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        Profiles = await profiles.GetProfilesAsync(cancellationToken);
        Changed?.Invoke();
    }

    public async Task<WebConnection> ConnectAsync(ConnectionProfile profile, CancellationToken cancellationToken)
    {
        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            [LogPropertyNames.ConnectionId] = profile.Id.Value, [LogPropertyNames.Ssh] = profile.Ssh.Enabled,
        });
        logger.LogDebug("Opening connection");
        try
        {
            var connection = await ConnectCoreAsync(profile, cancellationToken);
            logger.LogInformation("Connection opened");
            return connection;
        }
        catch (OperationCanceledException) { logger.LogInformation("Connection opening cancelled"); throw; }
        catch (Exception exception)
        {
            logger.LogWarning("Connection opening failed: {Failure}", ExceptionDiagnostics.Describe(exception));
            throw;
        }
    }

    private async Task<WebConnection> ConnectCoreAsync(ConnectionProfile profile, CancellationToken cancellationToken)
    {
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
        operation.CancelAfter(TimeSpan.FromSeconds(WebOperationTimeouts.ConnectionSeconds));
        await _gate.WaitAsync(operation.Token);
        try { return await OpenAsync(profile, operation.Token); }
        finally { _gate.Release(); }
    }

    public WebConnection Get(ConnectionId id) => _active.First(connection => connection.Profile.Id == id);

    public async Task ExpandAsync(WebConnection connection, WebDatabase database, CancellationToken cancellationToken)
    {
        var collections = await gateway.ListCollectionsAsync(connection.Target, DatabaseName.Create(database.Name), cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        database.Collections = collections.Order(StringComparer.OrdinalIgnoreCase).ToArray();
        database.Expanded = true;
        Changed?.Invoke();
    }

    public async Task RefreshDatabasesAsync(WebConnection connection, CancellationToken cancellationToken)
    {
        var databases = await gateway.ListDatabasesAsync(connection.Target, cancellationToken);
        var previous = connection.Databases.ToDictionary(database => database.Name, StringComparer.Ordinal);
        var refreshed = databases.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .Select(item => previous.GetValueOrDefault(item.Name) ?? new WebDatabase(item.Name)).ToList();
        var loaded = new Dictionary<WebDatabase, IReadOnlyList<string>>();
        foreach (var database in refreshed.Where(database => database.Expanded))
        {
            var collections = await gateway.ListCollectionsAsync(connection.Target, DatabaseName.Create(database.Name), cancellationToken);
            loaded[database] = collections.Order(StringComparer.OrdinalIgnoreCase).ToArray();
        }
        cancellationToken.ThrowIfCancellationRequested();
        foreach (var (database, collections) in loaded) database.Collections = collections;
        connection.Databases = refreshed;
        Changed?.Invoke();
    }

    public void ToggleConnection(WebConnection connection)
    {
        connection.Expanded = !connection.Expanded;
        Changed?.Invoke();
    }

    public async Task RevealCollectionAsync(CollectionSelection selection, CancellationToken cancellationToken)
    {
        var connection = Get(selection.ConnectionId);
        await RefreshDatabasesAsync(connection, cancellationToken);
        var database = connection.Databases.First(database => database.Name == selection.Database);
        await ExpandAsync(connection, database, cancellationToken);
        connection.Expanded = true;
        Changed?.Invoke();
    }

    public async Task DisconnectAsync(ConnectionId id)
    {
        var connection = _active.FirstOrDefault(item => item.Profile.Id == id);
        if (connection is null) return;
        _active.Remove(connection);
        await connection.Tunnel.DisposeAsync();
        logger.LogInformation("Connection {ConnectionId} closed", id.Value);
        Changed?.Invoke();
    }

    public async ValueTask DisposeAsync()
    {
        await _lifetime.CancelAsync();
        await _gate.WaitAsync();
        try
        {
            foreach (var connection in _active)
            {
                await connection.Tunnel.DisposeAsync();
                logger.LogDebug("Connection {ConnectionId} disposed", connection.Profile.Id.Value);
            }
            _active.Clear();
        }
        finally { _gate.Release(); _lifetime.Dispose(); }
    }

    private async Task<WebConnection> OpenAsync(ConnectionProfile profile, CancellationToken cancellationToken)
    {
        if (_active.FirstOrDefault(item => item.Profile.Id == profile.Id) is { } existing) return existing;
        var target = await profiles.ResolveTargetAsync(profile.Id, cancellationToken);
        var tunnel = await tunnels.OpenAsync(profile, target, cancellationToken);
        try
        {
            var connection = new WebConnection(profile, tunnel);
            await RefreshDatabasesAsync(connection, cancellationToken);
            _active.Add(connection);
            Changed?.Invoke();
            return connection;
        }
        catch { await tunnel.DisposeAsync(); throw; }
    }
}

public sealed class WebConnection(ConnectionProfile profile, TunnelConnection tunnel)
{
    public ConnectionProfile Profile { get; } = profile;
    public TunnelConnection Tunnel { get; } = tunnel;
    public MongoConnectionTarget Target => Tunnel.Target;
    public IReadOnlyList<WebDatabase> Databases { get; set; } = [];
    public bool Expanded { get; set; }
}

public sealed class WebDatabase(string name)
{
    public string Name { get; } = name;
    public bool Expanded { get; set; }
    public IReadOnlyList<string>? Collections { get; set; }
}
