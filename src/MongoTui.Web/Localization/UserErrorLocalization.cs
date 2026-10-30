using MongoTui.Application.Localization;

namespace MongoTui.Web.Localization;

internal static class UserErrorLocalization
{
    public static string UserError(this WebText text, Exception exception) =>
        text[ErrorLocalization.GetUserErrorKey(exception)];
}
