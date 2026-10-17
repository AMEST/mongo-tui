using MongoTui.Domain.Connections;

namespace MongoTui.Application.Services;

/// <summary>Separates URI credentials from the public profile before persistence or connection.</summary>
public static class ConnectionCredentials
{
    public static (ConnectionProfile Profile, string? Secret) Extract(ConnectionProfile profile, string? secret)
    {
        var parsed = ConnectionUriParser.Parse(profile.Uri);
        if (parsed.Username is null && parsed.Password is null) return (profile, secret);
        var auth = profile.Auth with
        {
            Kind = profile.Auth.Kind == AuthKind.None ? parsed.AuthKind : profile.Auth.Kind,
            Username = profile.Auth.Username ?? parsed.Username,
            AuthDatabase = profile.Auth.AuthDatabase ?? parsed.AuthDatabase,
        };
        return (profile with { Uri = parsed.Uri, Auth = auth }, secret ?? parsed.Password);
    }
}
