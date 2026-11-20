using LinqToDB.Mapping;

namespace MongoTui.Infrastructure.Persistence.Sqlite;

[Table("query_observations")]
public sealed class QueryObservationRow
{
    [Column("observation_id"), PrimaryKey]
    public string ObservationId { get; set; } = string.Empty;
    [Column("process_id")]
    public string ProcessId { get; set; } = string.Empty;
    [Column("connection_id")]
    public string ConnectionId { get; set; } = string.Empty;
    [Column("database")]
    public string? Database { get; set; }
    [Column("collection")]
    public string? Collection { get; set; }
    [Column("operation")]
    public int Operation { get; set; }
    [Column("source")]
    public int Source { get; set; }
    [Column("started_at")]
    public long StartedAt { get; set; }
    [Column("duration_ticks")]
    public long DurationTicks { get; set; }
    [Column("success")]
    public bool Success { get; set; }
    [Column("error_code")]
    public int? ErrorCode { get; set; }
    [Column("returned_count")]
    public long? ReturnedCount { get; set; }
    [Column("affected_count")]
    public long? AffectedCount { get; set; }
    [Column("docs_examined")]
    public long? DocumentsExamined { get; set; }
    [Column("keys_examined")]
    public long? KeysExamined { get; set; }
    [Column("slow")]
    public bool IsSlow { get; set; }
    [Column("timed_out")]
    public bool TimedOut { get; set; }
    [Column("cancelled")]
    public bool Cancelled { get; set; }
}
