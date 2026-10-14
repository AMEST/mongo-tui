using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using MongoTui.Application.Abstractions.Ssh;
using MongoTui.Domain.Exceptions;

namespace MongoTui.Infrastructure.Ssh;

internal sealed class SshCliTunnelSession : ISshTunnelSession
{
    private readonly System.Diagnostics.Process _process;
    private readonly Task _output;
    private readonly Task _errors;
    private string _failure = "SSH exited before the SOCKS proxy was ready.";
    private int _disposed;

    public SshCliTunnelSession(System.Diagnostics.Process process, int localPort)
    {
        _process = process;
        LocalPort = localPort;
        _output = process.StandardOutput.BaseStream.CopyToAsync(Stream.Null);
        _errors = ReadErrorsAsync();
    }

    public int LocalPort { get; }

    public async Task WaitUntilReadyAsync(Task authentication, CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (authentication.IsCompleted)
            {
                await authentication.ConfigureAwait(false);
            }

            await ThrowIfExitedAsync().ConfigureAwait(false);
            if (await ProbeSocksAsync(cancellationToken).ConfigureAwait(false))
            {
                await ThrowIfExitedAsync().ConfigureAwait(false);
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken).ConfigureAwait(false);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        try
        {
            if (!_process.HasExited)
            {
                try { _process.Kill(entireProcessTree: true); }
                catch (InvalidOperationException) { }
            }

            await _process.WaitForExitAsync().ConfigureAwait(false);
            await Task.WhenAll(_output, _errors).ConfigureAwait(false);
        }
        finally
        {
            _process.Dispose();
        }
    }

    private async Task<bool> ProbeSocksAsync(CancellationToken cancellationToken)
    {
        using var attempt = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        attempt.CancelAfter(TimeSpan.FromMilliseconds(300));
        try
        {
            using var client = new TcpClient(AddressFamily.InterNetwork);
            await client.ConnectAsync(IPAddress.Loopback, LocalPort, attempt.Token).ConfigureAwait(false);
            var stream = client.GetStream();
            await stream.WriteAsync(new byte[] { 5, 1, 0 }, attempt.Token).ConfigureAwait(false);
            var reply = new byte[2];
            await stream.ReadExactlyAsync(reply, attempt.Token).ConfigureAwait(false);
            return reply[0] == 5 && reply[1] == 0;
        }
        catch (Exception exception) when (exception is SocketException or IOException
            || exception is OperationCanceledException && !cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }

    private async Task ThrowIfExitedAsync()
    {
        if (_process.HasExited)
        {
            await _errors.ConfigureAwait(false);
            throw new ConnectionFailedException($"{_failure} Exit code: {_process.ExitCode}.");
        }
    }

    private async Task ReadErrorsAsync()
    {
        while (await _process.StandardError.ReadLineAsync().ConfigureAwait(false) is { } line)
        {
            if (line.Contains("Permission denied", StringComparison.OrdinalIgnoreCase))
                _failure = "SSH authentication failed. Check the SSH key and authentication response.";
            else if (line.Contains("Host key verification failed", StringComparison.OrdinalIgnoreCase))
                _failure = "SSH host key verification failed. Check the host's known_hosts entry.";
            else if (line.Contains("Address already in use", StringComparison.OrdinalIgnoreCase))
                _failure = "The local SOCKS port is already in use.";
            else if (line.Contains("Could not resolve hostname", StringComparison.OrdinalIgnoreCase))
                _failure = "The SSH host could not be resolved.";
            else if (line.Contains("Connection refused", StringComparison.OrdinalIgnoreCase))
                _failure = "The SSH host refused the connection.";
        }
    }
}
