using MongoTui.Domain.Connections;

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
    ConnectionProfile Profile,
    ActiveConnection? Owner = null,
    string? Database = null,
    string? Collection = null);
