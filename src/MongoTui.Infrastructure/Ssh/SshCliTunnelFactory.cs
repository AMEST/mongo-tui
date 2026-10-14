using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using MongoTui.Application.Abstractions.Ssh;
using MongoTui.Domain.Connections;
using MongoTui.Domain.Exceptions;

namespace MongoTui.Infrastructure.Ssh;

public sealed class SshCliTunnelFactory : ISshTunnelFactory
{
    private readonly SshCliOptions _options;
    private readonly SshAskPassFactory _askPass;

    public SshCliTunnelFactory(SshCliOptions options, SshAskPassFactory askPass)
    {
        _options = options;
        _askPass = askPass;
    }

    public async Task<ISshTunnelSession> StartAsync(
        SshTunnelOptions options,
        ITwoFactorPrompt prompt,
        CancellationToken cancellationToken)
    {
        options.Validate();
        cancellationToken.ThrowIfCancellationRequested();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_options.StartupTimeout);
        await using var askPass = _askPass.Start(prompt, timeout.Token);
        SshCliTunnelSession? session = null;
        try
        {
            var port = SelectPort(options.LocalPort);
            session = StartProcess(options, port, askPass.PipeName);
            await session.WaitUntilReadyAsync(askPass.Completion, timeout.Token).ConfigureAwait(false);
            return session;
        }
        catch (Exception exception)
        {
            if (session is not null)
            {
                await session.DisposeAsync().ConfigureAwait(false);
            }

            throw TranslateFailure(exception, timeout.IsCancellationRequested, cancellationToken);
        }
    }

    internal ProcessStartInfo BuildStartInfo(SshTunnelOptions options, int port, string pipeName)
    {
        var info = new ProcessStartInfo(_options.SshExecutable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in new[] { "-n", "-N", "-T", "-D", $"127.0.0.1:{port}",
            "-p", options.Port.ToString(CultureInfo.InvariantCulture),
            "-o", "ExitOnForwardFailure=yes", "-o", "BatchMode=no",
            "-o", "PreferredAuthentications=publickey,keyboard-interactive",
            "-o", "PubkeyAuthentication=yes",
            "-o", "PasswordAuthentication=no", "-o", "KbdInteractiveAuthentication=yes",
            "-o", "ControlMaster=no", "-o", "ControlPath=none", "-o", "ForkAfterAuthentication=no",
            "-o", "ServerAliveInterval=30", "-o", "ServerAliveCountMax=3" })
        {
            info.ArgumentList.Add(argument);
        }

        AddOptionalArgument(info, "-i", options.PrivateKeyFile);
        AddOptionalArgument(info, "-l", options.Username);
        info.ArgumentList.Add(options.Host!);
        info.Environment["SSH_ASKPASS"] = _options.AskPassExecutable;
        info.Environment["SSH_ASKPASS_REQUIRE"] = "force";
        info.Environment["MONGOTUI_ASKPASS_PIPE"] = pipeName;
        return info;
    }

    private SshCliTunnelSession StartProcess(SshTunnelOptions options, int port, string pipeName)
    {
        var process = new System.Diagnostics.Process { StartInfo = BuildStartInfo(options, port, pipeName) };
        try
        {
            process.Start();
            process.StandardInput.Close();
            return new SshCliTunnelSession(process, port);
        }
        catch
        {
            process.Dispose();
            throw;
        }
    }

    private static int SelectPort(int requestedPort)
    {
        using var listener = new TcpListener(IPAddress.Loopback, requestedPort);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private static void AddOptionalArgument(ProcessStartInfo info, string flag, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            info.ArgumentList.Add(flag);
            info.ArgumentList.Add(value);
        }
    }

    private static Exception TranslateFailure(Exception exception, bool timedOut, CancellationToken cancellationToken) =>
        exception switch
        {
            OperationCanceledException when timedOut && !cancellationToken.IsCancellationRequested =>
                new ConnectionFailedException("SSH authentication or SOCKS proxy startup timed out."),
            SocketException => new ConnectionFailedException("Could not bind the local SOCKS port. Choose an available port or use 0."),
            Win32Exception => new ConnectionFailedException("Could not start OpenSSH. Ensure ssh is installed and available in PATH."),
            _ => exception,
        };
}
