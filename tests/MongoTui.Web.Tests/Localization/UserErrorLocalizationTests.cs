using AwesomeAssertions;
using MongoTui.Application.Localization;
using MongoTui.Domain.Exceptions;
using MongoTui.Domain.Users;
using MongoTui.Web.Localization;

namespace MongoTui.Web.Tests.Localization;

public sealed class UserErrorLocalizationTests
{
    [Theory]
    [InlineData("en", UserManagementError.AlreadyExists, "User already exists.")]
    [InlineData("ru", UserManagementError.AlreadyExists, "Пользователь уже существует")]
    [InlineData("en", UserManagementError.InsufficientPrivileges, "Insufficient privileges to manage users.")]
    [InlineData("ru", UserManagementError.InsufficientPrivileges, "Недостаточно прав для управления пользователями")]
    [InlineData("en", UserManagementError.PasswordRequired, "Password must not be empty.")]
    [InlineData("ru", UserManagementError.PasswordRequired, "Пароль не должен быть пустым.")]
    public void Should_TranslateKnownFailure_WhenUserOperationFails(string language, UserManagementError code, string expected)
    {
        var text = new WebText();
        text.SetLanguage(language);

        var message = text.UserError(new UserManagementException(code));

        message.Should().Be(expected);
    }

    [Theory]
    [InlineData("en")]
    [InlineData("ru")]
    public void Should_ShowGenericFailure_WhenPlainDomainDiagnosticMatchesKnownMessage(string language)
    {
        var text = new WebText();
        text.SetLanguage(language);

        var message = text.UserError(new DomainException(UserErrorMessages.AlreadyExists));

        message.Should().Be(text[LocalizationKeys.Users.ErrorCommandFailed]);
    }

    [Theory]
    [InlineData("en", UserManagementError.AlreadyExists, "User already exists.")]
    [InlineData("ru", UserManagementError.AlreadyExists, "Пользователь уже существует")]
    [InlineData("en", UserManagementError.Unknown, "MongoDB command failed.")]
    [InlineData("ru", UserManagementError.Unknown, "Ошибка выполнения команды MongoDB")]
    [InlineData("en", (UserManagementError)int.MaxValue, "MongoDB command failed.")]
    [InlineData("ru", (UserManagementError)int.MaxValue, "Ошибка выполнения команды MongoDB")]
    public void Should_UseCodeWithoutExposingCause_WhenTypedFailureHasPrivateDiagnostic(string language, UserManagementError code, string expected)
    {
        var text = new WebText();
        text.SetLanguage(language);
        var cause = new InvalidOperationException("createUser pwd: secret-password mongodb://alice:secret-password@host");

        var message = text.UserError(new UserManagementException(code, cause));

        message.Should().Be(expected).And.NotContain("secret-password").And.NotContain("mongodb://");
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
