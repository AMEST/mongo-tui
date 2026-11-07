using LinqToDB;
using LinqToDB.Async;
using MongoTui.Application.Abstractions.Persistence;
using MongoTui.Domain.Diagnostics;
using MongoTui.Domain.Primitives;

namespace MongoTui.Infrastructure.Persistence.Sqlite;

public sealed class SqliteQueryMetricsRepository(SqliteDatabase database) : IQueryMetricsRepository
{
    public async Task AddBatchAsync(IReadOnlyList<QueryObservation> observations, CancellationToken cancellationToken)
    {
        await using var db = database.CreateConnection();
        await using var transaction = await db.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        foreach (var observation in observations)
            await db.InsertOrReplaceAsync(QueryObservationMapper.ToRow(observation), token: cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<QueryObservation>> QueryAsync(QueryMetricsFilter filter, int limit, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(limit);
        await using var db = database.CreateConnection();
        var query = db.QueryObservations.AsQueryable();
        if (filter.ConnectionId is { } id) query = query.Where(row => row.ConnectionId == id.ToString());
        if (filter.Database is { } name) query = query.Where(row => row.Database == name);
        if (filter.Collection is { } collection) query = query.Where(row => row.Collection == collection);
        if (filter.Operation is { } operation) query = query.Where(row => row.Operation == (int)operation);
        if (filter.Source is { } source) query = query.Where(row => row.Source == (int)source);
        if (filter.Since is { } since) query = query.Where(row => row.StartedAt >= since.UtcTicks);
        if (filter.MinimumDuration is { } duration) query = query.Where(row => row.DurationTicks >= duration.Ticks);
        if (filter.SlowOnly) query = query.Where(row => row.IsSlow);
        var rows = await query.OrderByDescending(row => row.StartedAt).Take(Math.Min(limit, 1000000))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        return rows.Select(QueryObservationMapper.FromRow).ToArray();
    }

    public async Task ClearAsync(ConnectionId? connectionId, CancellationToken cancellationToken)
    {
        await using var db = database.CreateConnection();
        var query = db.QueryObservations.AsQueryable();
        if (connectionId is not null) query = query.Where(row => row.ConnectionId == connectionId.ToString());
        await query.DeleteAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task PruneAsync(DateTimeOffset olderThan, int maximumRows, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumRows, 1);
        await using var db = database.CreateConnection();
        await db.QueryObservations.Where(row => row.StartedAt < olderThan.UtcTicks).DeleteAsync(cancellationToken).ConfigureAwait(false);
        var keep = db.QueryObservations.OrderByDescending(row => row.StartedAt).ThenBy(row => row.ObservationId)
            .Take(maximumRows).Select(row => row.ObservationId);
        await db.QueryObservations.Where(row => !keep.Contains(row.ObservationId)).DeleteAsync(cancellationToken).ConfigureAwait(false);
    }
}
