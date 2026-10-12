using System.Globalization;
using LinqToDB;
using LinqToDB.Async;
using MongoTui.Application.Abstractions.Persistence;
using MongoTui.Application.Models;
using MongoTui.Domain.Primitives;

namespace MongoTui.Infrastructure.Persistence.Sqlite;

/// <summary>LINQ to DB backed <see cref="IQueryHistoryRepository"/>.</summary>
public sealed class SqliteQueryHistoryRepository : IQueryHistoryRepository
{
    private readonly SqliteDatabase _database;

    public SqliteQueryHistoryRepository(SqliteDatabase database) => _database = database;

    public async Task AddAsync(QueryHistoryEntry entry, CancellationToken cancellationToken)
    {
        await using var db = _database.CreateConnection();

        await db.InsertAsync(ToRow(entry), token: cancellationToken);
    }

    public async Task<IReadOnlyList<QueryHistoryEntry>> GetRecentAsync(
        ConnectionId connectionId,
        int limit,
        CancellationToken cancellationToken)
    {
        await using var db = _database.CreateConnection();
        var key = connectionId.ToString();

        var rows = await db.QueryHistory
            .Where(entry => entry.ConnectionId == key)
            .OrderByDescending(entry => entry.RanAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return rows.Select(ToEntry).ToList();
    }

    public async Task ClearAsync(ConnectionId connectionId, CancellationToken cancellationToken)
    {
        await using var db = _database.CreateConnection();
        var key = connectionId.ToString();

        await db.QueryHistory
            .Where(entry => entry.ConnectionId == key)
            .DeleteAsync(cancellationToken);
    }

    private static QueryHistoryRow ToRow(QueryHistoryEntry entry) =>
        new()
        {
            ConnectionId = entry.ConnectionId.ToString(),
            Database = entry.Database,
            Collection = entry.Collection,
            Script = entry.Script,
            RanAt = FormatTimestamp(entry.RanAt),
            Success = entry.Success,
            ElapsedMs = entry.ElapsedMs,
        };

    private static QueryHistoryEntry ToEntry(QueryHistoryRow row) =>
        new()
        {
            Id = row.Id,
            ConnectionId = ConnectionId.Parse(row.ConnectionId),
            Database = row.Database,
            Collection = row.Collection,
            Script = row.Script,
            RanAt = ParseTimestamp(row.RanAt),
            Success = row.Success,
            ElapsedMs = row.ElapsedMs,
        };

    private static string FormatTimestamp(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    private static DateTimeOffset ParseTimestamp(string value) =>
        DateTimeOffset.Parse(value, CultureInfo.InvariantCulture);
}
