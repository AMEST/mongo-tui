using AwesomeAssertions;
using MongoTui.Application.Localization;
using MongoTui.Application.Models;
using MongoTui.Web.Localization;

namespace MongoTui.Web.Tests.Localization;

public sealed class WebTextTests
{
    [Theory]
    [InlineData("en", SecurityMode.None, "Local key file")]
    [InlineData("en", SecurityMode.Password, "Master password")]
    [InlineData("en", SecurityMode.Keyring, "System keychain")]
    [InlineData("ru", SecurityMode.None, "Локальный файл ключа")]
    [InlineData("ru", SecurityMode.Password, "Мастер-пароль")]
    [InlineData("ru", SecurityMode.Keyring, "Системное хранилище")]
    public void Should_ResolveModeName_WhenLanguageSelected(string language, SecurityMode mode, string expected)
    {
        var text = new WebText();
        text.SetLanguage(language);

        var name = text.SecurityModeName(mode);

        name.Should().Be(expected);
    }

    [Theory]
    [InlineData("ru", "en", "Connection lost")]
    [InlineData("en", "ru", "Нет связи с сервером")]
    public void Should_ReadRequestedLanguage_WhenSelectionMustRemainUnchanged(string selected, string requested, string expected)
    {
        var text = new WebText();
        text.SetLanguage(selected);

        var value = text.Get(LocalizationKeys.Reconnect.Title, requested);

        value.Should().Be(expected);
        text.Language.Should().Be(selected);
    }
}
