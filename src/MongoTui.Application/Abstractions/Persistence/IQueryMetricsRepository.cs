using MongoTui.Domain.Diagnostics;
using MongoTui.Domain.Primitives;

namespace MongoTui.Application.Abstractions.Persistence;

public interface IQueryMetricsRepository
{
    Task AddBatchAsync(IReadOnlyList<QueryObservation> observations, CancellationToken cancellationToken);
    Task<IReadOnlyList<QueryObservation>> QueryAsync(QueryMetricsFilter filter, int limit, CancellationToken cancellationToken);
    Task ClearAsync(ConnectionId? connectionId, CancellationToken cancellationToken);
    Task PruneAsync(DateTimeOffset olderThan, int maximumRows, CancellationToken cancellationToken);
}
