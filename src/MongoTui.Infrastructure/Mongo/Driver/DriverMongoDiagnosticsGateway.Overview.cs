using static MongoTui.Infrastructure.Mongo.Driver.MongoWireNames.Diagnostics;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoTui.Application.Models;
using MongoTui.Domain.Diagnostics;
using MongoTui.Domain.Exceptions;

namespace MongoTui.Infrastructure.Mongo.Driver;

public sealed partial class DriverMongoDiagnosticsGateway
{
    public async Task<ServerOverview> GetServerStatusAsync(MongoConnectionTarget target, CancellationToken cancellationToken)
    {
        var warnings = new List<string>();
        var status = await OptionalAsync(target, WireAdmin, new BsonDocument(WireServerStatus, 1), warnings, cancellationToken).ConfigureAwait(false);
        var replication = await OptionalAsync(target, WireAdmin, new BsonDocument(WireReplSetGetStatus, 1), warnings, cancellationToken).ConfigureAwait(false);
        var operations = await OptionalAsync(target, WireAdmin, new BsonDocument { { WireCurrentOp, 1 }, { WireActive, true },
            { WireSecsRunning, new BsonDocument(WireDollargte, 1) } }, warnings, cancellationToken).ConfigureAwait(false);
        var members = replication.GetValue(WireMembers, new BsonArray()).AsBsonArray.OfType<BsonDocument>()
            .Select(member => $"{Text(member, WireName)}: {Text(member, WireStateStr)}").ToArray();
        var current = operations.GetValue(WireInprog, new BsonArray()).AsBsonArray.OfType<BsonDocument>()
            .Select(row => new CurrentOperation(row.GetValue(WireOpid, "").ToString() ?? "", Text(row, WireNs), Text(row, WireOp),
                Number(row, WireSecsRunning) ?? 0, row.GetValue(WireWaitingForLock, false).ToBoolean())).ToArray();
        return new ServerOverview(Text(status, WireVersion), Number(status, WireUptime), Number(Doc(status, WireConnections), WireCurrent),
            Number(Doc(status, WireConnections), WireAvailable), Number(Doc(status, WireMem), WireResident) * 1024 * 1024,
            Text(Doc(status, WireStorageEngine), WireName), Doc(status, WireOpcounters).Elements.Where(e => e.Value.IsNumeric)
                .ToDictionary(e => e.Name, e => e.Value.ToInt64()), members, current, warnings);
    }

    private async Task<BsonDocument> OptionalAsync(MongoConnectionTarget target, string database, BsonDocument command,
        List<string> warnings, CancellationToken cancellationToken)
    {
        try { return await CommandAsync(target, database, command, cancellationToken).ConfigureAwait(false); }
        catch (DiagnosticsException exception) { warnings.Add(exception.Code == 76 ? "Diagnostics.ReplicationNotConfigured" : exception.Key); return new(); }
    }

    public async Task<DatabaseOverview> GetDatabaseOverviewAsync(MongoConnectionTarget target, string database, CancellationToken cancellationToken)
    {
        var stats = await CommandAsync(target, database, new BsonDocument(WireDbStats, 1), cancellationToken).ConfigureAwait(false);
        using var cursor = await Database(target, database).ListCollectionNamesAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        var names = await cursor.ToListAsync(cancellationToken).ConfigureAwait(false);
        var collections = new List<CollectionOverview>();
        foreach (var name in names.Order(StringComparer.Ordinal))
            collections.Add(await CollectionAsync(target, database, name, cancellationToken).ConfigureAwait(false));
        return new DatabaseOverview(database, Number(stats, WireObjects), Number(stats, WireDataSize), Number(stats, WireStorageSize), collections);
    }

    private async Task<CollectionOverview> CollectionAsync(MongoConnectionTarget target, string database, string name, CancellationToken cancellationToken)
    {
        try
        {
            var stats = await CommandAsync(target, database, new BsonDocument { { WireCollStats, name }, { WireScale, 1 } }, cancellationToken).ConfigureAwait(false);
            var (indexes, error) = await IndexUsageAsync(target, database, name, cancellationToken).ConfigureAwait(false);
            return new CollectionOverview(name, Number(stats, WireCount), Number(stats, WireSize), Number(stats, WireStorageSize),
                Number(stats, WireTotalIndexSize), indexes, error);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception exception) { return new CollectionOverview(name, null, null, null, null, [],
            exception is DiagnosticsException diagnostic ? diagnostic.Key : "Diagnostics.Error.Command"); }
    }

    private async Task<(IReadOnlyList<IndexUsage> Indexes, string? Error)> IndexUsageAsync(MongoConnectionTarget target,
        string database, string name, CancellationToken cancellationToken)
    {
        try
        {
            using var cursor = await Database(target, database).GetCollection<BsonDocument>(name)
                .AggregateAsync<BsonDocument>(new[] { new BsonDocument(WireDollarindexStats, new BsonDocument()) },
                    cancellationToken: cancellationToken).ConfigureAwait(false);
            var indexes = await cursor.ToListAsync(cancellationToken).ConfigureAwait(false);
            return (indexes.Select(index => new IndexUsage(Text(index, WireName) ?? "",
                    Number(Doc(index, WireAccesses), WireOps) ?? 0, Doc(index, WireAccesses).TryGetValue(WireSince, out var since)
                    && since.IsBsonDateTime ? since.ToUniversalTime() : null)).ToArray(), null);
        }
        catch (OperationCanceledException) { throw; }
        catch (MongoCommandException exception) { return ([], exception.Code == 13 ? "Diagnostics.Error.Unauthorized" : "Diagnostics.Error.Command"); }
        catch (Exception) { return ([], "Diagnostics.Error.Command"); }
    }
}
