using MongoTui.Application.Localization;
using MongoTui.Domain.Exceptions;

namespace MongoTui.Web.Localization;

internal static class UserErrorLocalization
{
    // Only known messages are exposed: driver errors can contain commands or credentials.
    public static string UserError(this WebText text, Exception exception) => text[exception switch
    {
        OperationCanceledException => LocalizationKeys.Users.ErrorCancelled,
        TimeoutException => LocalizationKeys.Users.ErrorTimeout,
        SecretsLockedException => LocalizationKeys.Error.Locked,
        DomainException => exception.Message switch
        {
            "Insufficient privileges to manage users." => LocalizationKeys.Users.ErrorUnauthorized,
            "User already exists." => LocalizationKeys.Users.ErrorAlreadyExists,
            "User not found." => LocalizationKeys.Users.ErrorNotFound,
            "Failed to grant role." => LocalizationKeys.Users.ErrorRoleGrantFailed,
            "Cannot delete the user of the current connection." => LocalizationKeys.Users.DeleteCurrentForbidden,
            "Username must not be empty." => LocalizationKeys.Users.UsernameRequired,
            "Password must not be empty." => LocalizationKeys.Users.PasswordRequired,
            "At least one database access or cluster role is required." => LocalizationKeys.Users.AccessRequired,
            "Connection is no longer active." => LocalizationKeys.Users.ErrorConnectionClosed,
            _ => LocalizationKeys.Users.ErrorCommandFailed,
        },
        _ => LocalizationKeys.Users.ErrorCommandFailed,
    }];
}
