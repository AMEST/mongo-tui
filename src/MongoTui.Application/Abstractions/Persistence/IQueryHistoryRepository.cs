using MongoTui.Application.Models;
using MongoTui.Domain.Primitives;

namespace MongoTui.Application.Abstractions.Persistence;

/// <summary>Persists executed-query history.</summary>
public interface IQueryHistoryRepository
{
    Task AddAsync(QueryHistoryEntry entry, CancellationToken cancellationToken);

    Task<IReadOnlyList<QueryHistoryEntry>> GetRecentAsync(
        ConnectionId connectionId,
        int limit,
        CancellationToken cancellationToken);

    Task ClearAsync(ConnectionId connectionId, CancellationToken cancellationToken);
}
