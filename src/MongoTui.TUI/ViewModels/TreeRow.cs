namespace MongoTui.TUI.ViewModels;

/// <summary>Kind of row in the connection tree.</summary>
public enum TreeRowKind
{
    Connection,
    Database,
    Collection,
}

/// <summary>A single row rendered in the connection tree.</summary>
public sealed record TreeRow(
    TreeRowKind Kind,
    string Text,
    ActiveConnection Owner,
    string? Database = null,
    string? Collection = null);
