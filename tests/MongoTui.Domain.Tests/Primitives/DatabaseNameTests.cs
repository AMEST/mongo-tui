using AwesomeAssertions;
using MongoTui.Domain.Exceptions;
using MongoTui.Domain.Primitives;

namespace MongoTui.Domain.Tests.Primitives;

public sealed class DatabaseNameTests
{
    [Theory]
    [InlineData("mydb")]
    [InlineData("my-db_2024")]
    [InlineData("Ωmega")]
    public void Should_Create_When_ValidName(string name)
    {
        var database = DatabaseName.Create(name);

        database.Value.Should().Be(name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ShouldThrow_DomainException_When_EmptyOrWhitespace(string name)
    {
        var act = () => DatabaseName.Create(name);

        act.Should().Throw<DomainException>();
    }

    [Theory]
    [InlineData("my/db")]
    [InlineData("my.db")]
    [InlineData("my db")]
    [InlineData("my$db")]
    [InlineData("my*db")]
    public void ShouldThrow_DomainException_When_ContainsForbiddenCharacter(string name)
    {
        var act = () => DatabaseName.Create(name);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void ShouldThrow_DomainException_When_ExceedsMaxBytes()
    {
        var name = new string('a', 65);

        var act = () => DatabaseName.Create(name);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Should_NotRenameDatabase_When_ToString()
    {
        var database = DatabaseName.Create("mydb");

        database.ToString().Should().Be("mydb");
    }
}
