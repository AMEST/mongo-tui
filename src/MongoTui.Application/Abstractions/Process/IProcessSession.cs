namespace MongoTui.Application.Abstractions.Process;

/// <summary>A running external process with redirected streams.</summary>
public interface IProcessSession : IDisposable
{
    StreamWriter StandardInput { get; }

    StreamReader StandardOutput { get; }

    StreamReader StandardError { get; }

    bool HasExited { get; }

    int ExitCode { get; }

    Task WaitForExitAsync(CancellationToken cancellationToken);

    void Kill();
}
