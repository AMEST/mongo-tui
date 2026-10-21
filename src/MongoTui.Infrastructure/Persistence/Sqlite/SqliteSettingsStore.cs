using LinqToDB;
using LinqToDB.Async;
using MongoTui.Application.Abstractions.Persistence;

namespace MongoTui.Infrastructure.Persistence.Sqlite;

/// <summary>LINQ to DB backed <see cref="ISettingsStore"/>.</summary>
public sealed class SqliteSettingsStore : ISettingsStore
{
    private readonly SqliteDatabase _database;

    public SqliteSettingsStore(SqliteDatabase database) => _database = database;

    public async Task<string?> GetAsync(string key, CancellationToken cancellationToken)
    {
        await using var session = _database.OpenSession();
        var db = session.Connection;

        return await db.Settings
            .Where(setting => setting.Key == key)
            .Select(setting => setting.Value)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task SetAsync(string key, string value, CancellationToken cancellationToken)
    {
        await using var session = _database.OpenSession();
        var db = session.Connection;

        await db.InsertOrReplaceAsync(new SettingRow { Key = key, Value = value }, token: cancellationToken);
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        await using var session = _database.OpenSession();
        var db = session.Connection;

        await db.Settings
            .Where(setting => setting.Key == key)
            .DeleteAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, string>> GetAllAsync(CancellationToken cancellationToken)
    {
        await using var session = _database.OpenSession();
        var db = session.Connection;

        var rows = await db.Settings.ToListAsync(cancellationToken);

        return rows.ToDictionary(row => row.Key, row => row.Value, StringComparer.Ordinal);
    }
}
