using AwesomeAssertions;
using MongoTui.Application.Services;

namespace MongoTui.Application.Tests.Services;

public sealed class ConnectionProxyUriTests
{
    [Theory]
    [InlineData("mongodb://host", "mongodb://host/?proxyHost=127.0.0.1&proxyPort=1080")]
    [InlineData("mongodb+srv://cluster.example.com", "mongodb+srv://cluster.example.com/?proxyHost=127.0.0.1&proxyPort=1080")]
    [InlineData("mongodb+srv://u:p%26w@cluster.example.com/app?proxyHost=old&proxyPort=42&authSource=admin", "mongodb+srv://u:p%26w@cluster.example.com/app?authSource=admin&proxyHost=127.0.0.1&proxyPort=1080")]
    [InlineData("mongodb://host/", "mongodb://host/?proxyHost=127.0.0.1&proxyPort=1080")]
    [InlineData("mongodb://host/db?tls=true", "mongodb://host/db?tls=true&proxyHost=127.0.0.1&proxyPort=1080")]
    [InlineData("mongodb://host?proxyHost=old&proxyPort=42&replicaSet=rs", "mongodb://host/?replicaSet=rs&proxyHost=127.0.0.1&proxyPort=1080")]
    [InlineData("mongodb://host/?PROXYHOST=a&proxyHost=b&proxyPort=1&ProxyPort=2", "mongodb://host/?proxyHost=127.0.0.1&proxyPort=1080")]
    [InlineData("mongodb://host/?proxy%48ost=old&proxyUsername=u&proxyPassword=p", "mongodb://host/?proxyHost=127.0.0.1&proxyPort=1080")]
    [InlineData("mongodb://u:p%26x@node1:27017,node2:27018/app?tls=true&replicaSet=rs&authSource=admin", "mongodb://u:p%26x@node1:27017,node2:27018/app?tls=true&replicaSet=rs&authSource=admin&proxyHost=127.0.0.1&proxyPort=1080")]
    public void Should_ReplaceProxyOptionsAndPreserveOtherComponents_WhenPreparingUri(string uri, string expected)
    {
        var result = ConnectionTunnelService.WithProxy(uri, 1080);

        result.Should().Be(expected);
    }
}
