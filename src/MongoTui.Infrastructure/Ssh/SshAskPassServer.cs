using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using MongoTui.Application.Abstractions.Ssh;

namespace MongoTui.Infrastructure.Ssh;

/// <summary>Routes OpenSSH prompts to the UI over a pipe restricted to the current user.</summary>
public sealed class SshAskPassServer : IAsyncDisposable
{
    private readonly ITwoFactorPrompt _prompt;
    private readonly CancellationTokenSource _lifetime;

    public SshAskPassServer(ITwoFactorPrompt prompt, CancellationToken cancellationToken)
    {
        _prompt = prompt;
        _lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Completion = ServeAsync(_lifetime.Token);
    }

    public string PipeName { get; } = "mt-" + Guid.NewGuid().ToString("N");

    public Task Completion { get; }

    public async ValueTask DisposeAsync()
    {
        await _lifetime.CancelAsync().ConfigureAwait(false);
        try
        {
            await Completion.ConfigureAwait(false);
        }
        catch (Exception) when (Completion.IsCanceled || Completion.IsFaulted)
        {
            // Startup observes prompt failures; cleanup must preserve the original exception.
        }

        _lifetime.Dispose();
    }

    private async Task ServeAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            await using var pipe = new NamedPipeServerStream(PipeName, PipeDirection.InOut, 1,
                PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            await pipe.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
            using var reader = new StreamReader(pipe, Encoding.UTF8, leaveOpen: true);
            await using var writer = new StreamWriter(pipe, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };
            var request = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            var prompt = request is null ? null : JsonSerializer.Deserialize<string>(request);
            if (prompt is null)
            {
                throw new IOException("SSH authentication helper sent an invalid request.");
            }

            var response = await _prompt.RequestAsync(prompt, cancellationToken).ConfigureAwait(false);
            await writer.WriteLineAsync(JsonSerializer.Serialize(response).AsMemory(), cancellationToken).ConfigureAwait(false);
            if (response is null)
            {
                throw new OperationCanceledException("SSH authentication was cancelled.");
            }
        }
    }
}
