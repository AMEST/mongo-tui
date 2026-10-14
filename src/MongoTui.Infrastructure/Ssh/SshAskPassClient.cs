using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace MongoTui.Infrastructure.Ssh;

/// <summary>The isolated helper entry point; writes only the response to OpenSSH's stdout pipe.</summary>
public sealed class SshAskPassClient
{
    public async Task<int> RunAsync(string pipeName, string prompt, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(2));
        try
        {
            await using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            await pipe.ConnectAsync(timeout.Token).ConfigureAwait(false);
            using var reader = new StreamReader(pipe, Encoding.UTF8, leaveOpen: true);
            await using var writer = new StreamWriter(pipe, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };
            await writer.WriteLineAsync(JsonSerializer.Serialize(prompt).AsMemory(), timeout.Token).ConfigureAwait(false);
            var response = await reader.ReadLineAsync(timeout.Token).ConfigureAwait(false);
            var answer = response is null ? null : JsonSerializer.Deserialize<string>(response);
            if (answer is null)
            {
                return 1;
            }

            await using var output = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false));
            await output.WriteLineAsync(answer.AsMemory(), timeout.Token).ConfigureAwait(false);
            return 0;
        }
        catch (Exception exception) when (exception is IOException or OperationCanceledException or JsonException or ArgumentException)
        {
            return 1;
        }
    }
}
