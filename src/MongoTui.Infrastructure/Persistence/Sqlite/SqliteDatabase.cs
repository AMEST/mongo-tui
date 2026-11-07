using Microsoft.Data.Sqlite;
using MongoTui.Application.Abstractions.Persistence;

namespace MongoTui.Infrastructure.Persistence.Sqlite;

/// <summary>Owns the SQLite connection string, creates data connections and applies migrations.</summary>
public sealed class SqliteDatabase : IStorageTransaction
{
    private readonly string _connectionString;
    private readonly AsyncLocal<MongoTuiDataConnection?> _transactionConnection = new();

    public SqliteDatabase(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);

        _connectionString = new SqliteConnectionStringBuilder { DataSource = databasePath }.ToString();
    }

    /// <summary>Creates a new LINQ to DB connection over the database.</summary>
    public MongoTuiDataConnection CreateConnection() => new(_connectionString);

    internal SqliteSession OpenSession() => _transactionConnection.Value is { } connection
        ? new SqliteSession(connection, false) : new SqliteSession(CreateConnection(), true);

    public async Task ExecuteAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken)
    {
        if (_transactionConnection.Value is not null)
            throw new InvalidOperationException("Nested storage transactions are not supported.");
        await using var connection = CreateConnection();
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        _transactionConnection.Value = connection;
        try
        {
            await action(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            await transaction.CommitAsync(cancellationToken);
        }
        finally
        {
            _transactionConnection.Value = null;
        }
    }

    /// <summary>Applies the schema migrations (idempotent).</summary>
    public void Migrate()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = MigrationSql;
        command.ExecuteNonQuery();
    }

    private const string MigrationSql =
        """
        CREATE TABLE IF NOT EXISTS connections (
            id               TEXT PRIMARY KEY,
            name             TEXT NOT NULL UNIQUE,
            uri              TEXT NOT NULL,
            auth_kind        TEXT NOT NULL,
            encrypted_secret BLOB,
            options_json     TEXT NOT NULL,
            created_at       TEXT NOT NULL,
            updated_at       TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS query_history (
            id            INTEGER PRIMARY KEY AUTOINCREMENT,
            connection_id TEXT NOT NULL,
            database      TEXT,
            collection    TEXT,
            script        TEXT NOT NULL,
            ran_at        TEXT NOT NULL,
            success       INTEGER NOT NULL,
            elapsed_ms    INTEGER
        );

        CREATE TABLE IF NOT EXISTS query_observations (
            observation_id TEXT PRIMARY KEY,
            process_id TEXT NOT NULL,
            connection_id TEXT NOT NULL,
            database TEXT,
            collection TEXT,
            operation INTEGER NOT NULL,
            source INTEGER NOT NULL,
            started_at INTEGER NOT NULL,
            duration_ticks INTEGER NOT NULL,
            success INTEGER NOT NULL,
            error_code INTEGER,
            returned_count INTEGER,
            affected_count INTEGER,
            docs_examined INTEGER,
            keys_examined INTEGER,
            slow INTEGER NOT NULL,
            timed_out INTEGER NOT NULL,
            cancelled INTEGER NOT NULL
        );
        CREATE INDEX IF NOT EXISTS ix_observations_started ON query_observations(started_at);
        CREATE INDEX IF NOT EXISTS ix_observations_scope ON query_observations(connection_id, database, collection, started_at);

        CREATE TABLE IF NOT EXISTS settings (
            key   TEXT PRIMARY KEY,
            value TEXT NOT NULL
        );
        """;
}
