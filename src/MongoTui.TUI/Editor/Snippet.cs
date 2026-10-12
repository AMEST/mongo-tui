namespace MongoTui.TUI.Editor;

/// <summary>A named query template that can be inserted into the editor.</summary>
public sealed record Snippet(string Title, string Body);
