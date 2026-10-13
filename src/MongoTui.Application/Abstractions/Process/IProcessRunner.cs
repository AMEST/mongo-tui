namespace MongoTui.Application.Abstractions.Process;

/// <summary>Runs external processes to completion and captures their output.</summary>
public interface IProcessRunner
{
    /// <summary>Runs a process to completion and captures its output.</summary>
    Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken);
}
