namespace MongoTui.Application.Abstractions.Process;

/// <summary>Starts and runs external processes.</summary>
public interface IProcessRunner
{
    /// <summary>Runs a process to completion and captures its output.</summary>
    Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken);

    /// <summary>Starts a long-lived process that can be driven interactively.</summary>
    Task<IProcessSession> StartAsync(ProcessRequest request, CancellationToken cancellationToken);
}
