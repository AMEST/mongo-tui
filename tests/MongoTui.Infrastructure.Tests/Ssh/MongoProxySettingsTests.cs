using AwesomeAssertions;
using MongoDB.Driver;
using MongoTui.Application.Services;

namespace MongoTui.Infrastructure.Tests.Ssh;

public sealed class MongoProxySettingsTests
{
    [Fact]
    public void Should_ConfigureSocksAndPreserveReplicaSet_WhenDriverParsesPreparedUri()
    {
        var uri = ConnectionTunnelService.WithProxy(
            "mongodb://bob:p%26w@node1:27017,node2:27018/app?replicaSet=rs&tls=true&authSource=admin&proxyHost=old&proxyPort=42", 1080);

        var settings = MongoClientSettings.FromConnectionString(uri);

        settings.Socks5ProxySettings.Host.Should().Be("127.0.0.1");
        settings.Socks5ProxySettings.Port.Should().Be(1080);
        settings.Servers.Select(server => server.ToString()).Should().Equal("node1:27017", "node2:27018");
        settings.ReplicaSetName.Should().Be("rs");
        settings.UseTls.Should().BeTrue();
        settings.Credential.Username.Should().Be("bob");
        settings.Credential.Source.Should().Be("admin");
    }
}
