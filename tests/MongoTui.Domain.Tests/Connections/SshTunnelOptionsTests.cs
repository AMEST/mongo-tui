using AwesomeAssertions;
using MongoTui.Domain.Connections;
using MongoTui.Domain.Exceptions;
using MongoTui.Domain.Primitives;

namespace MongoTui.Domain.Tests.Connections;

public sealed class SshTunnelOptionsTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("-oProxyCommand=bad")]
    [InlineData("user@host")]
    [InlineData("host\nname")]
    public void ShouldThrow_DomainException_WhenHostInvalid(string? host)
    {
        var options = new SshTunnelOptions { Enabled = true, Host = host };

        var act = options.Validate;

        act.Should().Throw<DomainException>();
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(65536, 0)]
    [InlineData(22, -1)]
    [InlineData(22, 65536)]
    public void ShouldThrow_DomainException_WhenPortInvalid(int port, int localPort)
    {
        var options = new SshTunnelOptions { Enabled = true, Host = "jump", Port = port, LocalPort = localPort };

        var act = options.Validate;

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void ShouldNot_ValidateUnusedFields_WhenDisabled()
    {
        var options = new SshTunnelOptions { Port = -1, LocalPort = -1 };

        var act = options.Validate;

        act.Should().NotThrow();
    }

    [Fact]
    public void Should_AcceptAgentAndAutomaticPort_WhenTpamLoginProvided()
    {
        var options = new SshTunnelOptions
        {
            Enabled = true,
            Host = "tpam",
            Port = 4422,
            Username = "requester+user@domain+system",
        };

        var act = options.Validate;

        act.Should().NotThrow();
        options.LocalPort.Should().Be(0);
        options.PrivateKeyFile.Should().BeNull();
    }

    [Fact]
    public void ShouldThrow_DomainException_WhenUsernameHasControlCharacters()
    {
        var options = new SshTunnelOptions { Enabled = true, Host = "jump", Username = "bob\n" };

        var act = options.Validate;

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Should_AcceptProfile_WhenSrvUriUsesTunnel()
    {
        var options = new SshTunnelOptions { Enabled = true, Host = "jump" };

        var profile = ConnectionProfile.Create(ConnectionId.New(), "srv", "mongodb+srv://cluster", ssh: options);

        profile.Uri.Should().Be("mongodb+srv://cluster");
        profile.Ssh.Should().Be(options);
    }
}
