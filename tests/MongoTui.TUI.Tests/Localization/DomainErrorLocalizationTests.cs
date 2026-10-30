using AwesomeAssertions;
using MongoTui.Application.Localization;
using MongoTui.Domain.Exceptions;
using MongoTui.Domain.Users;
using MongoTui.TUI.Localization;

namespace MongoTui.TUI.Tests.Localization;

public sealed class DomainErrorLocalizationTests
{
    [Theory]
    [InlineData("en")]
    [InlineData("ru")]
    public void Should_LocalizeUserFailures_WhenOperationFails(string language)
    {
        var localizer = new ResxLocalizer();
        localizer.SetLanguage(language);
        Exception[] failures = [new OperationCanceledException(), new TimeoutException(),
            new UserManagementException(UserManagementError.ConnectionClosed), new SecretsLockedException()];

        var messages = failures.Select(localizer.GetUserErrorMessage).ToArray();

        messages.Should().Equal(failures.Select(error => localizer.Get(ErrorLocalization.GetUserErrorKey(error))));
        messages.Should().OnlyContain(message => !message.StartsWith("Users.Error.") && message != "Error.Locked");
    }

    [Theory]
    [InlineData("en")]
    [InlineData("ru")]
    public void Should_ShowGenericFailure_WhenOnlyDiagnosticTextMatchesKnownReason(string language)
    {
        var localizer = new ResxLocalizer();
        localizer.SetLanguage(language);

        var message = localizer.GetUserErrorMessage(new DomainException(UserErrorMessages.AlreadyExists));

        message.Should().Be(localizer.Get(LocalizationKeys.Users.ErrorCommandFailed));
    }

    [Theory]
    [InlineData("en", false)]
    [InlineData("ru", false)]
    [InlineData("en", true)]
    [InlineData("ru", true)]
    public void Should_HideDiagnostic_WhenUserFailureIsUnknown(string language, bool domain)
    {
        var localizer = new ResxLocalizer();
        localizer.SetLanguage(language);
        const string diagnostic = "createUser pwd: secret-password mongodb://alice:secret-password@host";
        Exception error = domain ? new DomainException(diagnostic) : new InvalidOperationException(diagnostic);

        var message = localizer.GetUserErrorMessage(error);

        message.Should().Be(localizer.Get(LocalizationKeys.Users.ErrorCommandFailed));
        message.Should().NotContain("secret-password").And.NotContain("mongodb://");
    }

    [Fact]
    public void Should_PreserveValidationMessage_WhenConnectionFailureIsUnknown()
    {
        var localizer = new ResxLocalizer();
        var exception = new DomainException("Connection name must not be empty.");

        var message = localizer.GetErrorMessage(exception);

        message.Should().Be(exception.Message);
    }
}
