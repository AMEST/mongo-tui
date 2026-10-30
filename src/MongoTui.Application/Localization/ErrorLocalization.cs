using MongoTui.Domain.Exceptions;
using MongoTui.Domain.Users;

namespace MongoTui.Application.Localization;

/// <summary>Maps known failures to resource keys without exposing diagnostic text.</summary>
public static class ErrorLocalization
{
    public static string? GetErrorKey(Exception exception) => exception switch
    {
        OperationCanceledException => LocalizationKeys.Users.ErrorCancelled,
        TimeoutException => LocalizationKeys.Users.ErrorTimeout,
        SecretsLockedException => LocalizationKeys.Error.Locked,
        UserManagementException error => error.Code switch
        {
            UserManagementError.InsufficientPrivileges => LocalizationKeys.Users.ErrorUnauthorized,
            UserManagementError.AlreadyExists => LocalizationKeys.Users.ErrorAlreadyExists,
            UserManagementError.NotFound => LocalizationKeys.Users.ErrorNotFound,
            UserManagementError.RoleGrantFailed => LocalizationKeys.Users.ErrorRoleGrantFailed,
            UserManagementError.DeleteCurrentForbidden => LocalizationKeys.Users.DeleteCurrentForbidden,
            UserManagementError.CommandFailed => LocalizationKeys.Users.ErrorCommandFailed,
            UserManagementError.UsernameRequired => LocalizationKeys.Users.UsernameRequired,
            UserManagementError.PasswordRequired => LocalizationKeys.Users.PasswordRequired,
            UserManagementError.AccessRequired => LocalizationKeys.Users.AccessRequired,
            UserManagementError.ConnectionClosed => LocalizationKeys.Users.ErrorConnectionClosed,
            _ => null,
        },
        _ => null,
    };

    /// <summary>Uses a safe fallback for user commands, whose diagnostics can contain credentials.</summary>
    public static string GetUserErrorKey(Exception exception) =>
        GetErrorKey(exception) ?? LocalizationKeys.Users.ErrorCommandFailed;
}
