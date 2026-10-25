using System.Text.RegularExpressions;

namespace MongoTui.Web.Services;

internal static partial class SafeError
{
    private const int MaximumMessageCharacters = 1500;
    public static string Message(Exception exception)
    {
        var message = MongoUri().Replace(exception.Message, "mongodb://[redacted]");
        return message.Length > MaximumMessageCharacters ? message[..MaximumMessageCharacters] + "…" : message;
    }

    [GeneratedRegex(@"mongodb(?:\+srv)?://[^\s\""']+", RegexOptions.IgnoreCase)]
    private static partial Regex MongoUri();
}
