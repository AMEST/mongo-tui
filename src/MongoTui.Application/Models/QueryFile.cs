namespace MongoTui.Application.Models;

/// <summary>A JavaScript query read from or written to a file on disk.</summary>
public sealed record QueryFile(string Path, string Script);
