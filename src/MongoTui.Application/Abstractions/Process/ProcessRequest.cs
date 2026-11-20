namespace MongoTui.Application.Abstractions.Process;

/// <summary>Description of an external process to start.</summary>
public sealed record ProcessRequest(
    string FileName,
    IReadOnlyList<string> Arguments,
    string? WorkingDirectory = null,
    IReadOnlyDictionary<string, string>? Environment = null)
{
    /// <summary>Receives text as the process produces it. The two pipes can report concurrently.</summary>
    public Func<ProcessOutput, ValueTask>? OnOutput { get; init; }
}

public readonly record struct ProcessOutput(string Text, bool IsStandardError);
