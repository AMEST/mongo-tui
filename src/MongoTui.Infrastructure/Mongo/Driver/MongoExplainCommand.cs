using static MongoTui.Infrastructure.Mongo.Driver.MongoWireNames.Diagnostics;
using MongoDB.Bson;
using MongoTui.Domain.Diagnostics;

namespace MongoTui.Infrastructure.Mongo.Driver;

internal static class MongoExplainCommand
{
    public static BsonDocument Build(ExplainRequest request)
    {
        if (!Enum.IsDefined(request.Verbosity) || !Enum.IsDefined(request.Operation)) throw new ArgumentOutOfRangeException(nameof(request));
        var command = request.Operation == ExplainOperation.Aggregate
            ? new BsonDocument { { WireAggregate, request.Collection.Collection.Value },
                { WirePipeline, new BsonArray(request.Pipeline ?? []) }, { WireCursor, new BsonDocument() } }
            : new BsonDocument { { WireFind, request.Collection.Collection.Value }, { WireFilter, request.Filter ?? new BsonDocument() } };
        if (request.Pipeline?.Any(stage => stage.Contains(WireDollarout) || stage.Contains(WireDollarmerge)) == true)
            throw new ArgumentException("Write stages cannot be explained.");
        if (request.Sort is not null) command[WireSort] = request.Sort;
        if (request.Projection is not null) command[WireProjection] = request.Projection;
        if (request.Skip is { } skip) { ArgumentOutOfRangeException.ThrowIfNegative(skip); command[WireSkip] = skip; }
        if (request.Limit is { } limit) { ArgumentOutOfRangeException.ThrowIfNegative(limit); command[WireLimit] = limit; }
        if (request.Operation == ExplainOperation.FindOne) command[WireLimit] = 1;
        return new BsonDocument { { WireExplain, command }, { WireVerbosity, request.Verbosity switch
            { ExplainVerbosity.QueryPlanner => WireQueryPlanner, ExplainVerbosity.AllPlansExecution => WireAllPlansExecution, _ => WireExecutionStats } },
            { WireMaxTimeMS, (long)(request.MaxTime ?? TimeSpan.FromSeconds(30)).TotalMilliseconds } };
    }
}
