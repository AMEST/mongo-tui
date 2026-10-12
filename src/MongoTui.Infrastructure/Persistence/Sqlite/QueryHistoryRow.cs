using LinqToDB.Mapping;

namespace MongoTui.Infrastructure.Persistence.Sqlite;

/// <summary>Database row for the <c>query_history</c> table.</summary>
[Table("query_history")]
public sealed class QueryHistoryRow
{
    [Column("id"), PrimaryKey, Identity]
    public long Id { get; set; }

    [Column("connection_id")]
    public string ConnectionId { get; set; } = string.Empty;

    [Column("database")]
    public string? Database { get; set; }

    [Column("collection")]
    public string? Collection { get; set; }

    [Column("script")]
    public string Script { get; set; } = string.Empty;

    [Column("ran_at")]
    public string RanAt { get; set; } = string.Empty;

    [Column("success")]
    public bool Success { get; set; }

    [Column("elapsed_ms")]
    public long? ElapsedMs { get; set; }
}
