using System.Text.RegularExpressions;

namespace MongoTui.Web.Services;

internal static partial class SafeError
{
    private const int MaximumMessageCharacters = 1500;
    public static string Message(Exception exception) => Message(exception.Message);

    public static string Message(string diagnostic)
    {
        var message = MongoUri().Replace(diagnostic, "mongodb://[redacted]");
        return message.Length > MaximumMessageCharacters ? message[..MaximumMessageCharacters] + "…" : message;
    }

    [GeneratedRegex(@"mongodb(?:\+srv)?://[^\s\""']+", RegexOptions.IgnoreCase)]
    private static partial Regex MongoUri();
}
