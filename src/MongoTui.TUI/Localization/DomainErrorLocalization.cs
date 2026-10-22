using MongoTui.Application.Abstractions.Localization;
using MongoTui.Domain.Exceptions;

namespace MongoTui.TUI.Localization;

internal static class DomainErrorLocalization
{
    public static string GetErrorMessage(this ILocalizer localizer, DomainException exception) =>
        exception is SecretsLockedException ? localizer.Get("Error.Locked") : exception.Message;
}
