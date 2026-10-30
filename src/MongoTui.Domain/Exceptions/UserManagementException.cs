using MongoTui.Domain.Users;

namespace MongoTui.Domain.Exceptions;

/// <summary>A classified user-management failure with a safe diagnostic message.</summary>
public sealed class UserManagementException : DomainException
{
    public UserManagementException(UserManagementError code) : base(MessageFor(code)) => Code = code;

    public UserManagementException(UserManagementError code, Exception innerException)
        : base(MessageFor(code), innerException) => Code = code;

    public UserManagementError Code { get; }

    private static string MessageFor(UserManagementError code) => code switch
    {
        UserManagementError.InsufficientPrivileges => UserErrorMessages.InsufficientPrivileges,
        UserManagementError.AlreadyExists => UserErrorMessages.AlreadyExists,
        UserManagementError.NotFound => UserErrorMessages.NotFound,
        UserManagementError.RoleGrantFailed => UserErrorMessages.RoleGrantFailed,
        UserManagementError.DeleteCurrentForbidden => UserErrorMessages.DeleteCurrentForbidden,
        UserManagementError.UsernameRequired => UserErrorMessages.UsernameRequired,
        UserManagementError.PasswordRequired => UserErrorMessages.PasswordRequired,
        UserManagementError.AccessRequired => UserErrorMessages.AccessRequired,
        UserManagementError.ConnectionClosed => UserErrorMessages.ConnectionClosed,
        _ => UserErrorMessages.CommandFailed,
    };
}
