using Microsoft.Data.Sqlite;

namespace MongoTui.Infrastructure.Persistence.Sqlite;

/// <summary>Owns the SQLite connection string, creates data connections and applies migrations.</summary>
public sealed class SqliteDatabase
{
    private readonly string _connectionString;

    public SqliteDatabase(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);

        _connectionString = new SqliteConnectionStringBuilder { DataSource = databasePath }.ToString();
    }

    /// <summary>Creates a new LINQ to DB connection over the database.</summary>
    public MongoTuiDataConnection CreateConnection() => new(_connectionString);

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

        CREATE TABLE IF NOT EXISTS settings (
            key   TEXT PRIMARY KEY,
            value TEXT NOT NULL
        );
        """;
}
