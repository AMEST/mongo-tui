using MongoDB.Bson;
using MongoDB.Driver;
using MongoTui.Application.Models;
using Serilog;

namespace MongoTui.Infrastructure.Mongo.Driver;

/// <summary>Discovers stored databases and empty database namespaces known to the connection.</summary>
public sealed class MongoDatabaseDiscovery
{
    /// <summary>Includes URI, authentication and privilege namespaces missing from the server's database catalog.</summary>
    public async Task<IReadOnlyList<DatabaseInfo>> ListAsync(
        IMongoClient client,
        string? defaultDatabase,
        CancellationToken cancellationToken)
    {
        using var cursor = await client.ListDatabasesAsync(cancellationToken);
        var documents = await cursor.ToListAsync(cancellationToken);
        var databases = documents.Select(document => new DatabaseInfo(document["name"].AsString,
                document.TryGetValue("empty", out var empty) && empty.ToBoolean()))
            .ToDictionary(database => database.Name, StringComparer.Ordinal);
        IncludeEmpty(databases, defaultDatabase);
        if (client.Settings.Credential is null) return databases.Values.ToList();
        IncludeEmpty(databases, client.Settings.Credential.Source);

        try
        {
            var status = await client.GetDatabase("admin").RunCommandAsync<BsonDocument>(
                new BsonDocument { { "connectionStatus", 1 }, { "showPrivileges", true } },
                cancellationToken: cancellationToken);
            foreach (var name in AuthorizedDatabaseNames(status)) IncludeEmpty(databases, name);
        }
        catch (MongoCommandException exception)
        {
            Log.Warning("Could not discover authorized database namespaces (MongoDB error {Code})", exception.Code);
        }

        return databases.Values.ToList();
    }

    private static IEnumerable<string> AuthorizedDatabaseNames(BsonDocument status)
    {
        var auth = status.GetValue("authInfo", new BsonDocument()).AsBsonDocument;
        var privileges = auth.GetValue("authenticatedUserPrivileges", new BsonArray()).AsBsonArray;
        return privileges.Select(privilege => privilege["resource"].AsBsonDocument
            .GetValue("db", string.Empty).AsString);
    }

    private static void IncludeEmpty(IDictionary<string, DatabaseInfo> databases, string? name)
    {
        if (!string.IsNullOrWhiteSpace(name) && name != "$external")
        {
            databases.TryAdd(name, new DatabaseInfo(name, true));
        }
    }
}
