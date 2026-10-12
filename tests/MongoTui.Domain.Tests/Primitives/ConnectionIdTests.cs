using AwesomeAssertions;
using MongoTui.Domain.Exceptions;
using MongoTui.Domain.Primitives;

namespace MongoTui.Domain.Tests.Primitives;

public sealed class ConnectionIdTests
{
    [Fact]
    public void Should_CreateNonEmptyId_When_New()
    {
        var id = ConnectionId.New();

        id.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void Should_ReturnSameGuid_When_FromGuid()
    {
        var guid = Guid.NewGuid();

        var id = ConnectionId.From(guid);

        id.Value.Should().Be(guid);
    }

    [Fact]
    public void ShouldThrow_DomainException_When_EmptyGuid()
    {
        var act = () => ConnectionId.From(Guid.Empty);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void ShouldThrow_DomainException_When_InvalidString()
    {
        var act = () => ConnectionId.Parse("not-a-guid");

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Should_RoundTrip_When_ToStringAndParse()
    {
        var id = ConnectionId.New();

        var parsed = ConnectionId.Parse(id.ToString());

        parsed.Should().Be(id);
    }
}
