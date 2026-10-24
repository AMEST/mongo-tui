using AwesomeAssertions;
using MongoTui.Application.Services;

namespace MongoTui.Application.Tests.Services;

public sealed class PasswordGeneratorTests
{
    private readonly PasswordGenerator _sut = new();

    [Fact]
    public void Should_GenerateDefaultLength_When_NotSpecified()
    {
        var password = _sut.Generate();

        password.Should().HaveLength(24);
    }

    [Fact]
    public void Should_UseOnlyUnambiguousCharacters_When_Generated()
    {
        var password = _sut.Generate(64);

        password.All(character => !"O0Il1 ".Contains(character)).Should().BeTrue();
    }

    [Fact]
    public void Should_ContainEveryCharacterClass_When_Generated()
    {
        var password = _sut.Generate(32);

        password.Any(character => "abcdefghjkmnpqrstuvwxyz".Contains(character)).Should().BeTrue();
        password.Any(character => "ABCDEFGHJKMNPQRSTUVWXYZ".Contains(character)).Should().BeTrue();
        password.Any(character => "23456789".Contains(character)).Should().BeTrue();
        password.Any(character => "!@#$%^&*()-_=+[]{}:,.?".Contains(character)).Should().BeTrue();
    }

    [Fact]
    public void Should_NotRepeat_When_GeneratedMultipleTimes()
    {
        _sut.Generate().Should().NotBe(_sut.Generate());
    }
}
