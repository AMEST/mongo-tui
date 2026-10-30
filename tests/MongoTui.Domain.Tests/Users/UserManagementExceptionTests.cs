using AwesomeAssertions;
using MongoTui.Domain.Exceptions;
using MongoTui.Domain.Users;

namespace MongoTui.Domain.Tests.Users;

public sealed class UserManagementExceptionTests
{
    [Fact]
    public void Should_PreserveCauseWithoutExposingItsDiagnostic_WhenFailureContainsCredentials()
    {
        var cause = new InvalidOperationException("createUser pwd: secret mongodb://alice:secret@host");

        var error = new UserManagementException(UserManagementError.InsufficientPrivileges, cause);

        error.Code.Should().Be(UserManagementError.InsufficientPrivileges);
        error.InnerException.Should().BeSameAs(cause);
        error.Message.Should().Be(UserErrorMessages.InsufficientPrivileges).And.NotContain("secret");
    }

    [Theory]
    [InlineData(UserManagementError.Unknown)]
    [InlineData((UserManagementError)int.MaxValue)]
    public void Should_UseSafeDiagnostic_WhenReasonIsUnknown(UserManagementError code)
    {
        var error = new UserManagementException(code);

        error.Code.Should().Be(code);
        error.Message.Should().Be(UserErrorMessages.CommandFailed);
    }
}
