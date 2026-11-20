using MongoTui.Domain.Diagnostics;
using MongoTui.Domain.Primitives;
using MongoTui.Domain.Queries;

namespace MongoTui.Application.Abstractions.Diagnostics;

/// <summary>Observes execution without inspecting commands, results, or exception messages.</summary>
public interface IQueryMetricsRecorder
{
    Task<T> ObserveAsync<T>(ConnectionId? connectionId, string? database, string? collection,
        QueryOperationKind operation, QuerySource source, QueryPurpose purpose,
        Func<Task<T>> execute, Func<T, long?>? returnedCount = null, Func<T, long?>? affectedCount = null);
}
