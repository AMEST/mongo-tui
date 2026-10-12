namespace MongoTui.Application.Abstractions.Process;

/// <summary>Description of an external process to start.</summary>
public sealed record ProcessRequest(
    string FileName,
    IReadOnlyList<string> Arguments,
    string? WorkingDirectory = null,
    IReadOnlyDictionary<string, string>? Environment = null);
