using System.Net;
using System.Net.Sockets;
using AwesomeAssertions;
using FakeItEasy;
using MongoTui.Application.Abstractions.Ssh;
using MongoTui.Domain.Connections;
using MongoTui.Domain.Exceptions;
using MongoTui.Infrastructure.Ssh;

namespace MongoTui.Infrastructure.Tests.Ssh;

public sealed class SshCliTunnelFactoryTests
{
    private readonly ITwoFactorPrompt _prompt = A.Fake<ITwoFactorPrompt>();
    private readonly SshTunnelOptions _ssh = new()
    {
        Enabled = true,
        Host = "tpam",
        Port = 4422,
        Username = "requester+user@domain+system",
        PrivateKeyFile = "/keys/key with spaces",
    };

    [Fact]
    public void Should_UseLoopbackAndSeparateArguments_WhenBuildingCommand()
    {
        var factory = new SshCliTunnelFactory(new SshCliOptions("/app/mongo-tui"), new SshAskPassFactory());

        var info = factory.BuildStartInfo(_ssh, 1080, "private-pipe");

        info.UseShellExecute.Should().BeFalse();
        info.RedirectStandardInput.Should().BeTrue();
        info.RedirectStandardOutput.Should().BeTrue();
        info.RedirectStandardError.Should().BeTrue();
        info.ArgumentList.Should().ContainInOrder("-D", "127.0.0.1:1080", "-p", "4422");
        info.ArgumentList.Should().ContainInOrder("-i", "/keys/key with spaces", "-l", _ssh.Username!, "tpam");
        info.ArgumentList.Should().Contain("ExitOnForwardFailure=yes");
        info.ArgumentList.Should().Contain("ControlPath=none");
        info.ArgumentList.Should().NotContain("StrictHostKeyChecking=no");
        info.Environment["SSH_ASKPASS_REQUIRE"].Should().Be("force");
        info.Environment["MONGOTUI_ASKPASS_PIPE"].Should().Be("private-pipe");
        info.Environment["SSH_ASKPASS"].Should().Be("/app/mongo-tui");
    }

    [Fact]
    public async Task ShouldThrow_ConnectionFailedException_WhenOpenSshMissing()
    {
        var options = new SshCliOptions("helper") { SshExecutable = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")) };
        var factory = new SshCliTunnelFactory(options, new SshAskPassFactory());

        var act = async () => await factory.StartAsync(_ssh, _prompt, CancellationToken.None);

        await act.Should().ThrowAsync<ConnectionFailedException>().WithMessage("*OpenSSH*");
    }

    [Fact]
    public async Task ShouldThrow_ConnectionFailedException_WhenLocalPortOccupied()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var factory = new SshCliTunnelFactory(new SshCliOptions("helper"), new SshAskPassFactory());

        var act = async () => await factory.StartAsync(_ssh with { LocalPort = port }, _prompt, CancellationToken.None);

        await act.Should().ThrowAsync<ConnectionFailedException>().WithMessage("*local SOCKS port*");
    }
}
