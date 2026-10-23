using MongoTui.Application.Abstractions.Localization;
using MongoTui.Domain.Exceptions;

namespace MongoTui.TUI.Localization;

internal static class DomainErrorLocalization
{
    public static string GetErrorMessage(this ILocalizer localizer, DomainException exception)
    {
        if (exception is SecretsLockedException)
        {
            return localizer.Get("Error.Locked");
        }

        var message = exception.Message;
        if (message == "Insufficient privileges to manage users.")
        {
            return localizer.Get("Users.Error.Unauthorized");
        }
        if (message == "User already exists.")
        {
            return localizer.Get("Users.Error.AlreadyExists");
        }
        if (message == "User not found.")
        {
            return localizer.Get("Users.Error.NotFound");
        }
        if (message == "Failed to grant role.")
        {
            return localizer.Get("Users.Error.RoleGrantFailed");
        }
        if (message == "Cannot delete the user of the current connection.")
        {
            return localizer.Get("Users.DeleteCurrentForbidden");
        }
        if (message == "MongoDB command failed.")
        {
            return localizer.Get("Users.Error.CommandFailed");
        }
        if (message == "Username must not be empty.")
        {
            return localizer.Get("Users.UsernameRequired");
        }
        if (message == "Password must not be empty.")
        {
            return localizer.Get("Users.PasswordRequired");
        }
        if (message == "At least one database access or cluster role is required.")
        {
            return localizer.Get("Users.Access.Required");
        }

        return exception.Message;
    }
}
