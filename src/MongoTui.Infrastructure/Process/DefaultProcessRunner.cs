using System.Text;
using MongoTui.Application.Abstractions.Process;

namespace MongoTui.Infrastructure.Process;

/// <summary>Default <see cref="IProcessRunner"/> backed by <see cref="System.Diagnostics.Process"/>.</summary>
public sealed class DefaultProcessRunner : IProcessRunner
{
    public async Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var process = new System.Diagnostics.Process { StartInfo = BuildStartInfo(request) };
        process.Start();

        using var execution = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        using var stop = execution.Token.Register(() => Kill(process));
        var standardOutput = CaptureAsync(process.StandardOutput, request.OnOutput, false, execution);
        var standardError = CaptureAsync(process.StandardError, request.OnOutput, true, execution);
        await Task.WhenAll(process.WaitForExitAsync(execution.Token), standardOutput, standardError).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return new ProcessResult(process.ExitCode, await standardOutput.ConfigureAwait(false), await standardError.ConfigureAwait(false));
    }

    private static async Task<string> CaptureAsync(StreamReader reader, Func<ProcessOutput, ValueTask>? onOutput,
        bool isStandardError, CancellationTokenSource execution)
    {
        try
        {
            if (onOutput is null) return await reader.ReadToEndAsync(execution.Token).ConfigureAwait(false);

            var buffer = new char[4096];
            var text = new StringBuilder();
            var pending = 0;
            int count;
            while ((count = await reader.ReadAsync(buffer.AsMemory(pending), execution.Token).ConfigureAwait(false)) > 0)
            {
                count += pending;
                // Keep the two UTF-16 code units of a supplementary character in one chunk.
                pending = char.IsHighSurrogate(buffer[count - 1]) ? 1 : 0;
                var chunk = new string(buffer, 0, count - pending);
                if (pending > 0) buffer[0] = buffer[count - 1];
                if (chunk.Length == 0) continue;
                text.Append(chunk);
                await onOutput(new ProcessOutput(chunk, isStandardError)).ConfigureAwait(false);
            }
            return text.ToString();
        }
        catch when (!execution.IsCancellationRequested)
        {
            execution.Cancel();
            throw;
        }
    }

    private static System.Diagnostics.ProcessStartInfo BuildStartInfo(ProcessRequest request)
    {
        var startInfo = new System.Diagnostics.ProcessStartInfo(request.FileName)
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        foreach (var argument in request.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        if (request.WorkingDirectory is not null)
        {
            startInfo.WorkingDirectory = request.WorkingDirectory;
        }

        if (request.Environment is not null)
        {
            foreach (var (key, value) in request.Environment)
            {
                startInfo.Environment[key] = value;
            }
        }

        return startInfo;
    }

    private static void Kill(System.Diagnostics.Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
        }
    }
}
