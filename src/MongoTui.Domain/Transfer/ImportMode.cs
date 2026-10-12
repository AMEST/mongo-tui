namespace MongoTui.Domain.Transfer;

/// <summary>How imported documents are applied to the target collection.</summary>
public enum ImportMode
{
    Insert,
    Upsert,
    Merge,
    Drop,
}
