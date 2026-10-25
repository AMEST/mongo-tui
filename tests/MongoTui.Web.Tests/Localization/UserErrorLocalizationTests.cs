using AwesomeAssertions;
using MongoTui.Domain.Exceptions;
using MongoTui.Web.Localization;

namespace MongoTui.Web.Tests.Localization;

public sealed class UserErrorLocalizationTests
{
    [Theory]
    [InlineData("en", "User already exists.", "User already exists.")]
    [InlineData("ru", "User already exists.", "Пользователь уже существует")]
    [InlineData("en", "Insufficient privileges to manage users.", "Insufficient privileges to manage users.")]
    [InlineData("ru", "Insufficient privileges to manage users.", "Недостаточно прав для управления пользователями")]
    [InlineData("en", "Password must not be empty.", "Password must not be empty.")]
    [InlineData("ru", "Password must not be empty.", "Пароль не должен быть пустым.")]
    public void Should_TranslateKnownFailure_WhenUserOperationFails(string language, string error, string expected)
    {
        var text = new WebText();
        text.SetLanguage(language);

        var message = text.UserError(new DomainException(error));

        message.Should().Be(expected);
    }

    [Theory]
    [InlineData("en", false)]
    [InlineData("ru", false)]
    [InlineData("en", true)]
    [InlineData("ru", true)]
    public void Should_HideUnknownDiagnostic_WhenItMayContainCredentials(string language, bool domain)
    {
        const string diagnostic = "createUser: alice, pwd: secret-password, mongodb://alice:secret-password@host";
        var text = new WebText();
        text.SetLanguage(language);

        var message = text.UserError(domain ? new DomainException(diagnostic) : new InvalidOperationException(diagnostic));

        message.Should().Be(language == "ru" ? "Ошибка выполнения команды MongoDB" : "MongoDB command failed.");
        message.Should().NotContain("secret-password").And.NotContain("mongodb://");
    }
}
