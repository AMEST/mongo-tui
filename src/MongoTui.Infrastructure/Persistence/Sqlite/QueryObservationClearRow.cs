using LinqToDB.Mapping;

namespace MongoTui.Infrastructure.Persistence.Sqlite;

[Table("query_observation_clears")]
internal sealed class QueryObservationClearRow
{
    [Column("scope"), PrimaryKey]
    public string Scope { get; set; } = string.Empty;

    [Column("cleared_at")]
    public long ClearedAt { get; set; }
}
