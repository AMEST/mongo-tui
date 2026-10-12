namespace MongoTui.Application.Abstractions.Process;

/// <summary>Outcome of a finished external process.</summary>
public sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError)
{
    public bool Succeeded => ExitCode == 0;
}
