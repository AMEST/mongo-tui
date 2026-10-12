using AwesomeAssertions;
using MongoTui.TUI.Themes;
using Terminal.Gui.Configuration;

namespace MongoTui.TUI.Tests.Themes;

public sealed class DarkThemeTests
{
    [Fact]
    public void Should_NameScheme()
    {
        DarkTheme.Name.Should().Be("MongoTuiDark");
    }

    [Fact]
    public void Should_CreateScheme()
    {
        DarkTheme.Create().Should().NotBeNull();
    }

    [Fact]
    public void Should_RegisterScheme_When_RegisterCalled()
    {
        DarkTheme.Register();

        SchemeManager.GetScheme(DarkTheme.Name).Should().NotBeNull();
    }
}
