using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using AwesomeAssertions;
using MongoTui.Domain.Exceptions;
using MongoTui.Infrastructure.Ssh;

namespace MongoTui.Infrastructure.Tests.Ssh;

public sealed class SshCliTunnelSessionTests
{
    [Fact]
    public async Task Should_DeclareReady_WhenSocksHandshakeSucceeds()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var process = StartProcess(waitForInput: true);
        await using var session = new SshCliTunnelSession(process, ((IPEndPoint)listener.LocalEndpoint).Port);
        var reply = ReplyAsync(listener, [5, 0], timeout.Token);

        await session.WaitUntilReadyAsync(new TaskCompletionSource().Task, timeout.Token);

        (await reply).Should().Equal(5, 1, 0);
    }

    [Fact]
    public async Task ShouldNot_DeclareReady_WhenListenerIsNotSocks5()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(250));
        var process = StartProcess(waitForInput: true);
        await using var session = new SshCliTunnelSession(process, ((IPEndPoint)listener.LocalEndpoint).Port);
        var reply = ReplyAsync(listener, [5, 255], timeout.Token);

        var act = async () => await session.WaitUntilReadyAsync(new TaskCompletionSource().Task, timeout.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        (await reply).Should().Equal(5, 1, 0);
    }

    [Fact]
    public async Task Should_KillProcessAndReleaseResources_WhenDisposed()
    {
        var process = StartProcess(waitForInput: true);
        var pid = process.Id;
        var session = new SshCliTunnelSession(process, 1080);

        await session.DisposeAsync();

        await session.DisposeAsync();
        var act = () => System.Diagnostics.Process.GetProcessById(pid);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public async Task Should_ReportSanitizedFailure_WhenSshExits()
    {
        var process = StartProcess(waitForInput: false);
        await using var session = new SshCliTunnelSession(process, 1080);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await process.WaitForExitAsync(timeout.Token);

        var act = async () => await session.WaitUntilReadyAsync(new TaskCompletionSource().Task, timeout.Token);

        var failure = await act.Should().ThrowAsync<ConnectionFailedException>();
        failure.Which.Message.Should().Contain("authentication failed").And.NotContain("FAKE_SECRET");
    }

    private static System.Diagnostics.Process StartProcess(bool waitForInput)
    {
        var info = new ProcessStartInfo(OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/sh")
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        if (OperatingSystem.IsWindows())
        {
            info.ArgumentList.Add("/d");
            info.ArgumentList.Add("/c");
            info.ArgumentList.Add(waitForInput ? "set /p line=" : "echo Permission denied FAKE_SECRET 1>&2");
        }
        else
        {
            info.ArgumentList.Add("-c");
            info.ArgumentList.Add(waitForInput ? "read line" : "printf 'Permission denied FAKE_SECRET\n' >&2");
        }

        return System.Diagnostics.Process.Start(info)!;
    }

    private static async Task<byte[]> ReplyAsync(TcpListener listener, byte[] reply, CancellationToken cancellationToken)
    {
        using var client = await listener.AcceptTcpClientAsync(cancellationToken);
        var stream = client.GetStream();
        var greeting = new byte[3];
        await stream.ReadExactlyAsync(greeting, cancellationToken);
        await stream.WriteAsync(reply, cancellationToken);
        return greeting;
    }
}
