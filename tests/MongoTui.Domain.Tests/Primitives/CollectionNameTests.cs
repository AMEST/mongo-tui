using AwesomeAssertions;
using MongoTui.Domain.Exceptions;
using MongoTui.Domain.Primitives;

namespace MongoTui.Domain.Tests.Primitives;

public sealed class CollectionNameTests
{
    [Theory]
    [InlineData("users")]
    [InlineData("orders_2024")]
    [InlineData("my-collection")]
    public void Should_Create_When_ValidName(string name)
    {
        var collection = CollectionName.Create(name);

        collection.Value.Should().Be(name);
    }

    [Fact]
    public void ShouldThrow_DomainException_When_Empty()
    {
        var act = () => CollectionName.Create(string.Empty);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void ShouldThrow_DomainException_When_ContainsDollar()
    {
        var act = () => CollectionName.Create("user$data");

        act.Should().Throw<DomainException>();
    }

    [Theory]
    [InlineData("system.version")]
    [InlineData("system.sessions")]
    [InlineData("system.buckets.measurements")]
    public void Should_Create_When_ReferencingSystemCollection(string name)
    {
        var collection = CollectionName.Create(name);

        collection.Value.Should().Be(name);
    }

    [Theory]
    [InlineData("system.version")]
    [InlineData("system.sessions")]
    [InlineData("system.views")]
    public void ShouldThrow_DomainException_When_ValidatingSystemNameForCreation(string name)
    {
        var collection = CollectionName.Create(name);

        var act = () => collection.ValidateForCreation();

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void ShouldThrow_DomainException_When_ContainsNullCharacter()
    {
        var act = () => CollectionName.Create("us\0ers");

        act.Should().Throw<DomainException>();
    }
}
