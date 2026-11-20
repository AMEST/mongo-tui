using Microsoft.Extensions.Logging;
using MongoTui.Domain.Diagnostics;

namespace MongoTui.Application.Diagnostics;

/// <summary>Logs only allowlisted scalar metrics, never plans, queries, or exception objects.</summary>
public sealed class SlowQueryLogger(ILogger<SlowQueryLogger> logger)
{
    public void Write(QueryObservation observation) => logger.LogWarning(
        "Slow query: {Database}.{Collection} {Operation} took {DurationMs:0.0} ms "
        + "({Source}, returned {ReturnedCount}, docs examined {DocumentsExamined}, keys examined {KeysExamined})",
        observation.Database, observation.Collection, observation.Operation, observation.Duration.TotalMilliseconds,
        observation.Source, observation.ReturnedCount, observation.DocumentsExamined, observation.KeysExamined);
}
