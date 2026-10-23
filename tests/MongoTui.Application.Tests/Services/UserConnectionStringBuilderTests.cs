using AwesomeAssertions;
using MongoTui.Application.Services;
using MongoTui.Domain.Connections;
using MongoTui.Domain.Primitives;

namespace MongoTui.Application.Tests.Services;

public sealed class UserConnectionStringBuilderTests
{
    [Theory]
    [InlineData("mongodb://node:27017", "mongodb://bob:pw@node:27017?authSource=admin")]
    [InlineData("mongodb://node/app?tls=true&replicaSet=rs0", "mongodb://bob:pw@node/app?tls=true&replicaSet=rs0&authSource=admin")]
    [InlineData("mongodb+srv://cluster.example.com/?retryWrites=true", "mongodb+srv://bob:pw@cluster.example.com/?retryWrites=true&authSource=admin")]
    public void Should_BuildUriAndPreserveParameters_When_ProfileHasNoCredentials(string uri, string expected)
    {
        var profile = Profile(uri);

        var result = UserConnectionStringBuilder.Build(profile, "bob", "pw", "admin");

        result.Should().Be(expected);
    }

    [Fact]
    public void Should_RemoveOldCredentialsAndProxyParameters_When_ProfileUriContainsThem()
    {
        var profile = Profile("mongodb://old:secret@node/app?authSource=old&authMechanism=SCRAM-SHA-1&proxyHost=127.0.0.1&proxyPort=1080&tls=true");

        var result = UserConnectionStringBuilder.Build(profile, "bob", "pw", "app");

        result.Should().Be("mongodb://bob:pw@node/app?tls=true&authSource=app");
    }

    [Fact]
    public void Should_PercentEncodeUsernameAndPassword_When_SpecialCharactersPresent()
    {
        var profile = Profile("mongodb://node:27017");

        var result = UserConnectionStringBuilder.Build(profile, "bob@example.com", "p@:/?#[]%", "admin");

        result.Should().Be("mongodb://bob%40example.com:p%40%3A%2F%3F%23%5B%5D%25@node:27017?authSource=admin");
    }

    [Fact]
    public void Should_ReplaceExistingAuthSource_When_ProfileHasOne()
    {
        var profile = Profile("mongodb://node:27017/?authSource=users&tls=true");

        var result = UserConnectionStringBuilder.Build(profile, "bob", "pw", "orders");

        result.Should().Be("mongodb://bob:pw@node:27017/?tls=true&authSource=orders");
    }

    private static ConnectionProfile Profile(string uri) =>
        ConnectionProfile.Create(ConnectionId.New(), "test", uri);
}
