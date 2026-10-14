using AwesomeAssertions;
using MongoTui.Domain.Connections;
using MongoTui.Domain.Exceptions;
using MongoTui.Domain.Primitives;

namespace MongoTui.Domain.Tests.Connections;

public sealed class ConnectionProfileTests
{
    [Theory]
    [InlineData("mongodb://localhost:27017")]
    [InlineData("mongodb+srv://cluster.example.com")]
    [InlineData("mongodb://node1:27017,node2:27018/?replicaSet=rs")]
    [InlineData("mongodb://bob:pw@node1:27017,node2:27018/app")]
    [InlineData("mongodb://[::1]:27017,[::2]:27018/")]
    public void Should_Create_When_ValidMongoUri(string uri)
    {
        var profile = ConnectionProfile.Create(ConnectionId.New(), "local", uri);

        profile.Uri.Should().Be(uri);
    }

    [Theory]
    [InlineData("http://localhost:27017")]
    [InlineData("localhost:27017")]
    [InlineData("not a uri")]
    public void ShouldThrow_DomainException_When_InvalidScheme(string uri)
    {
        var act = () => ConnectionProfile.Create(ConnectionId.New(), "local", uri);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void ShouldThrow_DomainException_When_EmptyName()
    {
        var act = () => ConnectionProfile.Create(
            ConnectionId.New(),
            "  ",
            "mongodb://localhost:27017");

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Should_TrimName_When_WhitespaceAround()
    {
        var profile = ConnectionProfile.Create(
            ConnectionId.New(),
            "  local  ",
            "mongodb://localhost:27017");

        profile.Name.Should().Be("local");
    }

    [Fact]
    public void Should_AssignDefaultOptions_When_NoneProvided()
    {
        var profile = ConnectionProfile.Create(
            ConnectionId.New(),
            "local",
            "mongodb://localhost:27017");

        profile.Auth.Kind.Should().Be(AuthKind.None);
        profile.Tls.Enabled.Should().BeFalse();
        profile.Ssh.Enabled.Should().BeFalse();
    }
}
