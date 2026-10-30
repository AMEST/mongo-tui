using MongoTui.Domain.Primitives;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoTui.Application.Models;
using Microsoft.Extensions.Logging;

namespace MongoTui.Infrastructure.Mongo.Driver;

/// <summary>Discovers stored databases and empty database namespaces known to the connection.</summary>
public sealed class MongoDatabaseDiscovery(ILogger<MongoDatabaseDiscovery> logger)
{
    /// <summary>Includes URI, authentication and privilege namespaces missing from the server's database catalog.</summary>
    public async Task<IReadOnlyList<DatabaseInfo>> ListAsync(
        IMongoClient client,
        string? defaultDatabase,
        CancellationToken cancellationToken)
    {
        using var cursor = await client.ListDatabasesAsync(cancellationToken).ConfigureAwait(false);
        var documents = await cursor.ToListAsync(cancellationToken).ConfigureAwait(false);
        var databases = documents.Select(document => new DatabaseInfo(document[MongoWireNames.Fields.Name].AsString,
                document.TryGetValue(MongoWireNames.Fields.Empty, out var empty) && empty.ToBoolean()))
            .ToDictionary(database => database.Name, StringComparer.Ordinal);
        IncludeEmpty(databases, defaultDatabase);
        if (client.Settings.Credential is null) return databases.Values.ToList();
        IncludeEmpty(databases, client.Settings.Credential.Source);

        try
        {
            var status = await client.GetDatabase(MongoSystemNames.AdminDatabase).RunCommandAsync<BsonDocument>(
                new BsonDocument { { MongoWireNames.Commands.ConnectionStatus, 1 }, { MongoWireNames.Fields.ShowPrivileges, true } },
                cancellationToken: cancellationToken).ConfigureAwait(false);
            foreach (var name in AuthorizedDatabaseNames(status)) IncludeEmpty(databases, name);
        }
        catch (MongoCommandException exception)
        {
            logger.LogWarning("Could not discover authorized database namespaces (MongoDB error {Code})", exception.Code);
        }

        return databases.Values.ToList();
    }

    private static IEnumerable<string> AuthorizedDatabaseNames(BsonDocument status)
    {
        var auth = status.GetValue(MongoWireNames.Fields.AuthInfo, new BsonDocument()).AsBsonDocument;
        var privileges = auth.GetValue(MongoWireNames.Fields.AuthenticatedUserPrivileges, new BsonArray()).AsBsonArray;
        return privileges.Select(privilege => privilege[MongoWireNames.Fields.Resource].AsBsonDocument
            .GetValue(MongoWireNames.Fields.Database, string.Empty).AsString);
    }

    private static void IncludeEmpty(IDictionary<string, DatabaseInfo> databases, string? name)
    {
        if (!string.IsNullOrWhiteSpace(name) && name != "$external")
        {
            databases.TryAdd(name, new DatabaseInfo(name, true));
        }
    }
}
