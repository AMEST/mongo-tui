using AwesomeAssertions;
using MongoTui.TUI.Localization;

namespace MongoTui.TUI.Tests.Localization;

public sealed class ResxLocalizerTests
{
    private readonly ResxLocalizer _sut = new();

    [Fact]
    public void Should_ReturnEnglishString_When_DefaultCulture()
    {
        _sut.SetLanguage("en");

        _sut.Get("App.Title").Should().Be("MongoTui");
        _sut.Get("Menu.File").Should().Be("File");
    }

    [Fact]
    public void Should_ReturnRussianString_When_LanguageRu()
    {
        _sut.SetLanguage("ru");

        _sut.Get("Menu.File").Should().Be("Файл");
        _sut.CurrentLanguage.Should().Be("ru");
    }

    [Fact]
    public void Should_FormatArguments_When_KeyHasPlaceholders()
    {
        _sut.SetLanguage("en");

        var text = _sut.Format("Connections.ConfirmDelete", "local");

        text.Should().Be("Delete connection \"local\"?");
    }

    [Fact]
    public void Should_ReturnKey_When_KeyMissing()
    {
        _sut.Get("does.not.exist").Should().Be("does.not.exist");
    }
}
