namespace MongoTui.Domain.Transfer;

/// <summary>Output format for an export operation.</summary>
public enum ExportFormat
{
    Json,
    CanonicalJson,
    Csv,
    Bson,
}
