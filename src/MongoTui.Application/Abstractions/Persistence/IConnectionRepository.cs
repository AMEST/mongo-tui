using MongoTui.Application.Models;
using MongoTui.Domain.Primitives;

namespace MongoTui.Application.Abstractions.Persistence;

/// <summary>Persists connection profiles and their encrypted secrets.</summary>
public interface IConnectionRepository
{
    Task<IReadOnlyList<StoredConnection>> GetAllAsync(CancellationToken cancellationToken);

    Task<StoredConnection?> GetAsync(ConnectionId id, CancellationToken cancellationToken);

    Task AddAsync(StoredConnection connection, CancellationToken cancellationToken);

    Task UpdateAsync(StoredConnection connection, CancellationToken cancellationToken);

    Task DeleteAsync(ConnectionId id, CancellationToken cancellationToken);
}
