using static MongoTui.Infrastructure.Mongo.Driver.MongoWireNames.Diagnostics;
using System.Collections.Frozen;
using MongoDB.Bson;
using MongoTui.Domain.Diagnostics;

namespace MongoTui.Infrastructure.Mongo.Driver;

internal static class MongoProfilerReader
{
    private static readonly FrozenDictionary<string, QueryOperationKind> Operations =
        new Dictionary<string, QueryOperationKind>(StringComparer.Ordinal)
        {
            [WireQuery] = QueryOperationKind.Find,
            [WireGetmore] = QueryOperationKind.Find,
            [WireCommand] = QueryOperationKind.Command,
            [WireInsert] = QueryOperationKind.Insert,
            [WireUpdate] = QueryOperationKind.Update,
            [WireRemove] = QueryOperationKind.Delete,
        }.ToFrozenDictionary(StringComparer.Ordinal);

    public static BsonDocument Filter(string database, ProfilerReadFilter filter)
    {
        var duration = new BsonDocument(WireDollargte, filter.MinimumDuration.TotalMilliseconds);
        if (filter.SlowThresholdMs is { } threshold) duration[WireDollargt] = threshold;
        var query = new BsonDocument(WireMillis, duration);
        if (filter.Since is { } timestamp) query[WireTs] = new BsonDocument(WireDollargte, timestamp.UtcDateTime);
        if (filter.Collection is { } collection) query[WireNs] = database + "." + collection;
        if (filter.Operation is { } operation)
        {
            query[WireOp] = operation == QueryOperationKind.Other
                ? new BsonDocument(WireDollarnin, new BsonArray(Operations.Keys))
                : new BsonDocument(WireDollarin, new BsonArray(Operations.Where(pair => pair.Value == operation).Select(pair => pair.Key)));
        }
        return query;
    }

    public static BsonDocument Projection() => new()
    {
        { "_id", 0 }, { WireTs, 1 }, { WireOp, 1 }, { WireNs, 1 }, { WireMillis, 1 },
        { WireDocsExamined, 1 }, { WireKeysExamined, 1 }, { WireNreturned, 1 }, { WireErrCode, 1 }, { WirePlanSummary, 1 },
    };

    private static string? SafePlan(string value)
    {
        string[] stages = [WireCOLLSCAN, WireIXSCAN, WireIDHACK, WireCLUSTEREDIXSCAN, WireDISTINCTSCAN];
        return stages.FirstOrDefault(stage => value == stage || value.StartsWith(stage + " ", StringComparison.Ordinal));
    }

    public static ProfilerEntry Map(BsonDocument row)
    {
        var ns = row.GetValue(WireNs, "").AsString.Split('.', 2);
        return new ProfilerEntry
        {
            Timestamp = row.GetValue(WireTs, new BsonDateTime(DateTime.UnixEpoch)).ToUniversalTime(),
            Database = ns[0], Collection = ns.Length > 1 ? ns[1] : null,
            Operation = Operations.GetValueOrDefault(row.GetValue(WireOp, "").AsString, QueryOperationKind.Other),
            Duration = TimeSpan.FromMilliseconds(DriverMongoDiagnosticsGateway.Number(row, WireMillis) ?? 0),
            DocumentsExamined = DriverMongoDiagnosticsGateway.Number(row, WireDocsExamined),
            KeysExamined = DriverMongoDiagnosticsGateway.Number(row, WireKeysExamined),
            ReturnedCount = DriverMongoDiagnosticsGateway.Number(row, WireNreturned),
            PlanSummary = SafePlan(row.GetValue(WirePlanSummary, "").AsString),
            ErrorCode = (int?)DriverMongoDiagnosticsGateway.Number(row, WireErrCode),
        };
    }
}
