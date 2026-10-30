using MongoTui.Domain.Connections;
using MongoTui.Application.Models;

namespace MongoTui.Application.Services;

/// <summary>Separates URI credentials from the public profile before persistence or connection.</summary>
public static class ConnectionCredentials
{
    public static (ConnectionProfile Profile, string? Secret) Extract(ConnectionProfile profile, string? secret)
    {
        var parsed = ConnectionUriParser.Parse(profile.Uri);
        if (parsed.Username is null && parsed.Password is null) return (profile, secret);
        var (auth, password) = Merge(parsed, profile.Auth, secret);
        return (profile with { Uri = parsed.Uri, Auth = auth }, password);
    }

    /// <summary>Prefers filled fields, then URI credentials, then saved values.</summary>
    public static (AuthOptions Auth, string? Secret) Merge(ParsedConnectionUri parsed,
        AuthOptions? fields, string? password, AuthOptions? savedAuth = null, string? savedSecret = null)
    {
        var auth = new AuthOptions
        {
            Kind = fields is { Kind: not AuthKind.None } ? fields.Kind
                : parsed.AuthKind != AuthKind.None ? parsed.AuthKind : fields?.Kind ?? savedAuth?.Kind ?? AuthKind.None,
            Username = NonEmpty(fields?.Username) ?? parsed.Username ?? savedAuth?.Username,
            AuthDatabase = NonEmpty(fields?.AuthDatabase) ?? parsed.AuthDatabase ?? savedAuth?.AuthDatabase,
        };
        return (auth, NonEmpty(password) ?? parsed.Password ?? savedSecret);
    }

    private static string? NonEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;
}
