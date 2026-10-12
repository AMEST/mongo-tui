using LinqToDB.Mapping;

namespace MongoTui.Infrastructure.Persistence.Sqlite;

/// <summary>Database row for the <c>settings</c> table.</summary>
[Table("settings")]
public sealed class SettingRow
{
    [Column("key"), PrimaryKey]
    public string Key { get; set; } = string.Empty;

    [Column("value")]
    public string Value { get; set; } = string.Empty;
}
