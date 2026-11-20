using MongoTui.Application.Abstractions.Diagnostics;
using MongoTui.Application.Services;
using MongoTui.Domain.Diagnostics;
using MongoTui.Domain.Exceptions;
using MongoTui.Domain.Primitives;
using MongoTui.Domain.Queries;

namespace MongoTui.Application.Diagnostics;

public sealed class QueryMetricsRecorder(IQueryMetricsStore store, DiagnosticsSettingsService settings,
    SlowQueryLogger logger, TimeProvider timeProvider) : IQueryMetricsRecorder
{
    private readonly Guid _processId = Guid.NewGuid();

    public async Task<T> ObserveAsync<T>(ConnectionId? connectionId, string? database, string? collection,
        QueryOperationKind operation, QuerySource source, QueryPurpose purpose,
        Func<Task<T>> execute, Func<T, long?>? returnedCount = null, Func<T, long?>? affectedCount = null)
    {
        var snapshot = settings.Current;
        if (connectionId is null || purpose == QueryPurpose.Internal
            || (!snapshot.MetricsEnabled && !snapshot.LogSlowQueries))
            return await execute().ConfigureAwait(false);
        var started = timeProvider.GetUtcNow();
        var timestamp = timeProvider.GetTimestamp();
        T? result = default;
        Exception? failure = null;
        try { result = await execute().ConfigureAwait(false); return result; }
        catch (Exception exception) { failure = exception; throw; }
        finally
        {
            TryRecord(new QueryObservation
            {
                ProcessId = _processId, ConnectionId = connectionId, Database = database, Collection = collection,
                Operation = operation, Source = source, StartedAt = started,
                Duration = timeProvider.GetElapsedTime(timestamp), Success = failure is null,
                TimedOut = failure is QueryTimeoutException or TimeoutException,
                Cancelled = failure is OperationCanceledException,
            }, snapshot, result, returnedCount, affectedCount);
        }
    }

    private void TryRecord<T>(QueryObservation observation, ClientDiagnosticsSettings snapshot, T? result,
        Func<T, long?>? returnedCount, Func<T, long?>? affectedCount)
    {
        try
        {
            observation = observation with
            {
                IsSlow = observation.Duration.TotalMilliseconds > snapshot.SlowThresholdMs,
                ReturnedCount = observation.Success ? returnedCount?.Invoke(result!) : null,
                AffectedCount = observation.Success ? affectedCount?.Invoke(result!) : null,
            };
            if (snapshot.MetricsEnabled) store.Record(observation);
            if (snapshot.LogSlowQueries && observation.IsSlow) logger.Write(observation);
        }
        catch (Exception)
        {
            // Diagnostics are best-effort and must preserve the query result or original failure.
        }
    }
}
