using static MongoTui.Infrastructure.Mongo.Driver.MongoWireNames.Diagnostics;
using MongoDB.Bson;
using MongoTui.Domain.Diagnostics;

namespace MongoTui.Infrastructure.Mongo.Driver;

internal static class MongoProfilerReader
{
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
            Operation = row.GetValue(WireOp, "").AsString switch
            {
                WireQuery or WireGetmore => QueryOperationKind.Find, WireCommand => QueryOperationKind.Command,
                WireInsert => QueryOperationKind.Insert, WireUpdate => QueryOperationKind.Update,
                WireRemove => QueryOperationKind.Delete, _ => QueryOperationKind.Other,
            },
            Duration = TimeSpan.FromMilliseconds(DriverMongoDiagnosticsGateway.Number(row, WireMillis) ?? 0),
            DocumentsExamined = DriverMongoDiagnosticsGateway.Number(row, WireDocsExamined),
            KeysExamined = DriverMongoDiagnosticsGateway.Number(row, WireKeysExamined),
            ReturnedCount = DriverMongoDiagnosticsGateway.Number(row, WireNreturned),
            PlanSummary = SafePlan(row.GetValue(WirePlanSummary, "").AsString),
            ErrorCode = (int?)DriverMongoDiagnosticsGateway.Number(row, WireErrCode),
        };
    }
}
