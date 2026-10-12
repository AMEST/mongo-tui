using LinqToDB.Mapping;

namespace MongoTui.Infrastructure.Persistence.Sqlite;

/// <summary>Database row for the <c>connections</c> table.</summary>
[Table("connections")]
public sealed class ConnectionRow
{
    [Column("id"), PrimaryKey]
    public string Id { get; set; } = string.Empty;

    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("uri")]
    public string Uri { get; set; } = string.Empty;

    [Column("auth_kind")]
    public string AuthKind { get; set; } = string.Empty;

    [Column("encrypted_secret")]
    public byte[]? EncryptedSecret { get; set; }

    [Column("options_json")]
    public string OptionsJson { get; set; } = string.Empty;

    [Column("created_at")]
    public string CreatedAt { get; set; } = string.Empty;

    [Column("updated_at")]
    public string UpdatedAt { get; set; } = string.Empty;
}
