using AwesomeAssertions;
using MongoTui.Application.Localization;
using MongoTui.Domain.Exceptions;
using MongoTui.Domain.Users;

namespace MongoTui.Application.Tests.Localization;

public sealed class ErrorLocalizationTests
{
    [Theory]
    [InlineData(UserManagementError.InsufficientPrivileges, LocalizationKeys.Users.ErrorUnauthorized)]
    [InlineData(UserManagementError.AlreadyExists, LocalizationKeys.Users.ErrorAlreadyExists)]
    [InlineData(UserManagementError.NotFound, LocalizationKeys.Users.ErrorNotFound)]
    [InlineData(UserManagementError.RoleGrantFailed, LocalizationKeys.Users.ErrorRoleGrantFailed)]
    [InlineData(UserManagementError.DeleteCurrentForbidden, LocalizationKeys.Users.DeleteCurrentForbidden)]
    [InlineData(UserManagementError.CommandFailed, LocalizationKeys.Users.ErrorCommandFailed)]
    [InlineData(UserManagementError.UsernameRequired, LocalizationKeys.Users.UsernameRequired)]
    [InlineData(UserManagementError.PasswordRequired, LocalizationKeys.Users.PasswordRequired)]
    [InlineData(UserManagementError.AccessRequired, LocalizationKeys.Users.AccessRequired)]
    [InlineData(UserManagementError.ConnectionClosed, LocalizationKeys.Users.ErrorConnectionClosed)]
    public void Should_ResolveResourceKey_WhenUserFailureIsClassified(UserManagementError code, string expectedKey)
    {
        var key = ErrorLocalization.GetErrorKey(new UserManagementException(code));

        key.Should().Be(expectedKey);
    }

    [Theory]
    [InlineData(UserErrorMessages.InsufficientPrivileges)]
    [InlineData(UserErrorMessages.AlreadyExists)]
    [InlineData(UserErrorMessages.NotFound)]
    [InlineData(UserErrorMessages.RoleGrantFailed)]
    [InlineData(UserErrorMessages.DeleteCurrentForbidden)]
    [InlineData(UserErrorMessages.CommandFailed)]
    [InlineData(UserErrorMessages.UsernameRequired)]
    [InlineData(UserErrorMessages.PasswordRequired)]
    [InlineData(UserErrorMessages.AccessRequired)]
    [InlineData(UserErrorMessages.ConnectionClosed)]
    public void ShouldNot_ClassifyFailure_WhenOnlyDiagnosticMessageMatches(string message)
    {
        var error = new DomainException(message);

        var key = ErrorLocalization.GetErrorKey(error);

        key.Should().BeNull();
        ErrorLocalization.GetUserErrorKey(error).Should().Be(LocalizationKeys.Users.ErrorCommandFailed);
    }

    [Theory]
    [InlineData(UserManagementError.Unknown)]
    [InlineData((UserManagementError)int.MaxValue)]
    public void Should_UseSafeFallback_WhenUserCodeIsUnknown(UserManagementError code)
    {
        var error = new UserManagementException(code);

        var key = ErrorLocalization.GetUserErrorKey(error);

        key.Should().Be(LocalizationKeys.Users.ErrorCommandFailed);
        ErrorLocalization.GetErrorKey(error).Should().BeNull();
    }

    [Fact]
    public void Should_UseOuterCode_WhenInnerDiagnosticDescribesDifferentFailure()
    {
        var error = new UserManagementException(UserManagementError.AlreadyExists,
            new UserManagementException(UserManagementError.NotFound));

        var key = ErrorLocalization.GetErrorKey(error);

        key.Should().Be(LocalizationKeys.Users.ErrorAlreadyExists);
    }

    [Theory]
    [InlineData(0, LocalizationKeys.Users.ErrorCancelled)]
    [InlineData(1, LocalizationKeys.Users.ErrorTimeout)]
    [InlineData(2, LocalizationKeys.Error.Locked)]
    public void Should_ResolveResourceKey_WhenFailureHasKnownType(int kind, string expectedKey)
    {
        Exception exception = kind switch
        {
            0 => new TaskCanceledException(),
            1 => new TimeoutException(),
            _ => new SecretsLockedException(),
        };

        var key = ErrorLocalization.GetErrorKey(exception);

        key.Should().Be(expectedKey);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Should_UseSafeUserFallback_WhenDiagnosticIsUnknown(bool domain)
    {
        const string message = "mongodb://alice:secret@host";
        Exception exception = domain ? new DomainException(message) : new InvalidOperationException(message);

        var key = ErrorLocalization.GetUserErrorKey(exception);

        key.Should().Be(LocalizationKeys.Users.ErrorCommandFailed);
        ErrorLocalization.GetErrorKey(exception).Should().BeNull();
    }
}
