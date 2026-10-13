using MongoTui.Application.Abstractions.Localization;
using MongoTui.Application.Abstractions.Mongo;
using MongoTui.Application.Models;
using MongoTui.Domain.Connections;
using MongoTui.Domain.Primitives;

namespace MongoTui.TUI.ViewModels;

/// <summary>Model of the active-connections tree: connections, databases and collections.</summary>
public sealed class TreeViewModel : ObservableObject
{
    private const string ExpandedMarker = "\u2212";
    private const string CollapsedMarker = "+";

    private readonly IMongoGateway _gateway;
    private readonly ILocalizer _localizer;
    private readonly List<ActiveConnection> _connections = [];
    private readonly HashSet<ConnectionId> _collapsed = [];
    private readonly HashSet<(ConnectionId ConnectionId, string Database)> _expandedDatabases = [];
    private readonly Dictionary<(ConnectionId ConnectionId, string Database), IReadOnlyList<string>> _collections = [];

    public TreeViewModel(IMongoGateway gateway, ILocalizer localizer)
    {
        _gateway = gateway;
        _localizer = localizer;
    }

    /// <summary>Currently open connections.</summary>
    public IReadOnlyList<ActiveConnection> Connections => _connections;

    /// <summary>Number of open connections.</summary>
    public int Count => _connections.Count;

    /// <summary>Adds a connection or replaces the existing one with the same profile id.</summary>
    public void AddOrReplace(ActiveConnection connection)
    {
        _connections.RemoveAll(existing => existing.Profile.Id == connection.Profile.Id);
        _connections.Add(connection);
    }

    /// <summary>Removes an active connection together with its cached expand state and collections.</summary>
    public void Remove(ConnectionId connectionId)
    {
        _connections.RemoveAll(connection => connection.Profile.Id == connectionId);
        _collapsed.Remove(connectionId);

        foreach (var key in _expandedDatabases.Where(key => key.ConnectionId == connectionId).ToList())
        {
            _expandedDatabases.Remove(key);
            _collections.Remove(key);
        }
    }

    /// <summary>Replaces the database list of an existing connection.</summary>
    public void UpdateDatabases(ConnectionId connectionId, IReadOnlyList<DatabaseInfo> databases)
    {
        var index = _connections.FindIndex(connection => connection.Profile.Id == connectionId);
        if (index >= 0)
        {
            _connections[index] = _connections[index] with { Databases = databases };
        }
    }

    /// <summary>Whether a connection node is expanded (shows its databases).</summary>
    public bool IsExpanded(ConnectionId connectionId) => !_collapsed.Contains(connectionId);

    /// <summary>Whether a database node is expanded (shows its collections).</summary>
    public bool IsExpanded(ConnectionId connectionId, string database) =>
        _expandedDatabases.Contains((connectionId, database));

    /// <summary>Toggles the expanded state of a connection node.</summary>
    public void ToggleConnection(ConnectionId connectionId)
    {
        if (!_collapsed.Remove(connectionId))
        {
            _collapsed.Add(connectionId);
        }
    }

    /// <summary>Toggles a database node, loading its collections on first expansion.</summary>
    public async Task ToggleDatabaseAsync(
        ConnectionId connectionId,
        MongoConnectionTarget target,
        string database,
        CancellationToken cancellationToken)
    {
        var key = (connectionId, database);

        if (_expandedDatabases.Remove(key))
        {
            return;
        }

        if (!_collections.ContainsKey(key))
        {
            var names = await _gateway.ListCollectionsAsync(target, DatabaseName.Create(database), cancellationToken);
            _collections[key] = names.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToList();
        }

        _expandedDatabases.Add(key);
    }

    /// <summary>Reloads the cached collections of a database.</summary>
    public async Task ReloadCollectionsAsync(
        ConnectionId connectionId,
        MongoConnectionTarget target,
        string database,
        CancellationToken cancellationToken)
    {
        var key = (connectionId, database);
        var names = await _gateway.ListCollectionsAsync(target, DatabaseName.Create(database), cancellationToken);
        _collections[key] = names.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>Removes a database and its cached collections from the tree.</summary>
    public void RemoveDatabase(ConnectionId connectionId, string database)
    {
        var key = (connectionId, database);
        _expandedDatabases.Remove(key);
        _collections.Remove(key);

        var index = _connections.FindIndex(connection => connection.Profile.Id == connectionId);

        if (index >= 0)
        {
            _connections[index] = _connections[index] with
            {
                Databases = _connections[index].Databases.Where(info => info.Name != database).ToList(),
            };
        }
    }

    /// <summary>Removes a single collection from the cache.</summary>
    public void RemoveCollection(ConnectionId connectionId, string database, string collection)
    {
        var key = (connectionId, database);

        if (_collections.TryGetValue(key, out var collections))
        {
            _collections[key] = collections.Where(name => name != collection).ToList();
        }
    }

    /// <summary>Flattens the tree into display rows honouring expand state.</summary>
    public IReadOnlyList<TreeRow> BuildRows()
    {
        var rows = new List<TreeRow>();

        foreach (var connection in _connections)
        {
            var id = connection.Profile.Id;
            var connectionExpanded = IsExpanded(id);
            rows.Add(new TreeRow(
                TreeRowKind.Connection,
                $"{(connectionExpanded ? ExpandedMarker : CollapsedMarker)} {connection.Profile.Name}",
                connection));

            if (!connectionExpanded)
            {
                continue;
            }

            foreach (var database in connection.Databases)
            {
                var expanded = IsExpanded(id, database.Name);
                var suffix = database.IsEmpty ? $" {_localizer.Get("Database.Empty")}" : string.Empty;
                rows.Add(new TreeRow(
                    TreeRowKind.Database,
                    $"  {(expanded ? ExpandedMarker : CollapsedMarker)} {database.Name}{suffix}",
                    connection,
                    database.Name));

                if (expanded && _collections.TryGetValue((id, database.Name), out var collections))
                {
                    rows.AddRange(collections.Select(collection =>
                        new TreeRow(TreeRowKind.Collection, $"    {collection}", connection, database.Name, collection)));
                }
            }
        }

        return rows;
    }
}
