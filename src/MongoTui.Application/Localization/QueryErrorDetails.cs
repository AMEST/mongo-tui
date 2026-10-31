using System.Globalization;
using System.Text.RegularExpressions;
using MongoTui.Domain.Exceptions;

namespace MongoTui.Application.Localization;

/// <summary>Formats query failures for display, preserving causes and redacting connection URIs.</summary>
public static partial class QueryErrorDetails
{
    private const int MaximumMessageCharacters = 1500;

    public static string GetMessage(Exception exception, string failureTemplate)
    {
        var cause = exception;
        for (var depth = 0; depth < 16 && cause is QueryExecutionException { InnerException: { } inner }; depth++)
            cause = inner;
        var message = cause is DomainException ? cause.Message
            : string.Format(CultureInfo.InvariantCulture, failureTemplate, $"{cause.GetType().Name}: {cause.Message}");
        message = MongoUri().Replace(message, "mongodb://[redacted]");
        return message.Length > MaximumMessageCharacters ? message[..MaximumMessageCharacters] + "…" : message;
    }

    [GeneratedRegex(@"mongodb(?:\+srv)?://[^\s\""']+", RegexOptions.IgnoreCase)]
    private static partial Regex MongoUri();
}
