namespace MongoTui.Web.Services;

/// <summary>An atomic snapshot of the editor document and its selected fragment, if any.</summary>
public sealed record EditorExecution(string Script, string? Selection);
