using MongoTui.Application.Localization;
using MongoTui.Application.Abstractions.Localization;
using MongoTui.Domain.Exceptions;

namespace MongoTui.TUI.Localization;

internal static class DomainErrorLocalization
{
    public static string GetErrorMessage(this ILocalizer localizer, DomainException exception) =>
        ErrorLocalization.GetErrorKey(exception) is { } key ? localizer.Get(key) : exception.Message;

    public static string GetUserErrorMessage(this ILocalizer localizer, Exception exception) =>
        localizer.Get(ErrorLocalization.GetUserErrorKey(exception));
}
