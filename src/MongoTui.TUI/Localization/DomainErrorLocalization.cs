using MongoTui.Application.Localization;
using MongoTui.Application.Abstractions.Localization;
using MongoTui.Domain.Exceptions;

namespace MongoTui.TUI.Localization;

internal static class DomainErrorLocalization
{
    public static string GetErrorMessage(this ILocalizer localizer, DomainException exception)
    {
        if (exception is SecretsLockedException)
        {
            return localizer.Get(LocalizationKeys.Error.Locked);
        }

        var message = exception.Message;
        if (message == "Insufficient privileges to manage users.")
        {
            return localizer.Get(LocalizationKeys.Users.ErrorUnauthorized);
        }
        if (message == "User already exists.")
        {
            return localizer.Get(LocalizationKeys.Users.ErrorAlreadyExists);
        }
        if (message == "User not found.")
        {
            return localizer.Get(LocalizationKeys.Users.ErrorNotFound);
        }
        if (message == "Failed to grant role.")
        {
            return localizer.Get(LocalizationKeys.Users.ErrorRoleGrantFailed);
        }
        if (message == "Cannot delete the user of the current connection.")
        {
            return localizer.Get(LocalizationKeys.Users.DeleteCurrentForbidden);
        }
        if (message == "MongoDB command failed.")
        {
            return localizer.Get(LocalizationKeys.Users.ErrorCommandFailed);
        }
        if (message == "Username must not be empty.")
        {
            return localizer.Get(LocalizationKeys.Users.UsernameRequired);
        }
        if (message == "Password must not be empty.")
        {
            return localizer.Get(LocalizationKeys.Users.PasswordRequired);
        }
        if (message == "At least one database access or cluster role is required.")
        {
            return localizer.Get(LocalizationKeys.Users.AccessRequired);
        }

        return exception.Message;
    }
}
