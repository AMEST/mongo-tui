using AwesomeAssertions;
using MongoTui.Application.Services;
using MongoTui.Domain.Connections;
using MongoTui.Domain.Exceptions;

namespace MongoTui.Application.Tests.Services;

public sealed class ConnectionUriParserTests
{
    [Fact]
    public void Should_ParseCredentials_When_UriHasUserInfo()
    {
        var parsed = ConnectionUriParser.Parse("mongodb://bob:pw@localhost:27017/app?authSource=admin");

        parsed.Uri.Should().Be("mongodb://localhost:27017/app?authSource=admin");
        parsed.Username.Should().Be("bob");
        parsed.Password.Should().Be("pw");
        parsed.Database.Should().Be("app");
        parsed.AuthDatabase.Should().Be("admin");
        parsed.AuthKind.Should().Be(AuthKind.ScramSha256);
        parsed.Host.Should().Be("localhost");
    }

    [Fact]
    public void Should_UsePathAsAuthSource_When_NoAuthSourceParameter()
    {
        var parsed = ConnectionUriParser.Parse("mongodb://localhost:27017/app");

        parsed.AuthDatabase.Should().Be("app");
    }

    [Theory]
    [InlineData("mongodb://localhost/?authSource=admin", null)]
    [InlineData("mongodb://localhost:27017", null)]
    [InlineData("mongodb://host1,host2/orders%2Darchive?authSource=admin", "orders-archive")]
    [InlineData("mongodb+srv://cluster.example/catalog?authSource=admin", "catalog")]
    public void Should_ParseDatabaseSeparately_WhenUriIncludesAuthenticationSource(string uri, string? expected)
    {
        var parsed = ConnectionUriParser.Parse(uri);

        parsed.Database.Should().Be(expected);
    }

    [Fact]
    public void Should_MapMechanism_When_AuthMechanismPresent()
    {
        var parsed = ConnectionUriParser.Parse("mongodb://u:p@localhost/?authMechanism=SCRAM-SHA-1");

        parsed.AuthKind.Should().Be(AuthKind.ScramSha1);
    }

    [Fact]
    public void Should_ReturnNoneKind_When_NoUsername()
    {
        var parsed = ConnectionUriParser.Parse("mongodb://localhost:27017");

        parsed.AuthKind.Should().Be(AuthKind.None);
        parsed.Username.Should().BeNull();
    }

    [Fact]
    public void Should_ParseSrvScheme()
    {
        var parsed = ConnectionUriParser.Parse("mongodb+srv://user:secret@cluster.example.com/app");

        parsed.Host.Should().Be("cluster.example.com");
        parsed.Uri.Should().Be("mongodb+srv://cluster.example.com/app");
        parsed.Username.Should().Be("user");
        parsed.Password.Should().Be("secret");
    }

    [Fact]
    public void Should_DecodeUrlEncodedPassword()
    {
        var parsed = ConnectionUriParser.Parse("mongodb://bob:p%40ss@localhost:27017");

        parsed.Password.Should().Be("p@ss");
    }

    [Fact]
    public void Should_KeepQueryParameters_When_Present()
    {
        var parsed = ConnectionUriParser.Parse("mongodb://localhost:27017/?retryWrites=true");

        parsed.Uri.Should().Contain("retryWrites=true");
    }

    [Theory]
    [InlineData("mongodb://alice:password@localhost/?authSource=admin", "mongodb://localhost/?authSource=admin")]
    [InlineData("mongodb+srv://alice:password@cluster.example/", "mongodb+srv://cluster.example/")]
    public void Should_PreserveRootSlash_When_RemovingCredentials(string uri, string expected)
    {
        var parsed = ConnectionUriParser.Parse(uri);

        parsed.Uri.Should().Be(expected);
    }

    [Theory]
    [InlineData("http://localhost")]
    [InlineData("localhost:27017")]
    [InlineData("")]
    public void ShouldThrow_DomainException_When_InvalidScheme(string uri)
    {
        var act = () => ConnectionUriParser.Parse(uri);

        act.Should().Throw<DomainException>();
    }
}
