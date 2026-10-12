using MongoTui.Application.Abstractions.Process;

namespace MongoTui.Infrastructure.Process;

/// <summary>Wraps a started <see cref="System.Diagnostics.Process"/> as an <see cref="IProcessSession"/>.</summary>
internal sealed class ProcessSession : IProcessSession
{
    private readonly System.Diagnostics.Process _process;

    public ProcessSession(System.Diagnostics.Process process)
    {
        _process = process;
        StandardInput = process.StandardInput;
        StandardOutput = process.StandardOutput;
        StandardError = process.StandardError;
    }

    public StreamWriter StandardInput { get; }

    public StreamReader StandardOutput { get; }

    public StreamReader StandardError { get; }

    public bool HasExited => _process.HasExited;

    public int ExitCode => _process.ExitCode;

    public Task WaitForExitAsync(CancellationToken cancellationToken) =>
        _process.WaitForExitAsync(cancellationToken);

    public void Kill()
    {
        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
        }
    }

    public void Dispose()
    {
        Kill();
        _process.Dispose();
    }
}
