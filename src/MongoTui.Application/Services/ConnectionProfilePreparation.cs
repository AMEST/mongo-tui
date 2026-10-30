using MongoTui.Application.Models;
using MongoTui.Domain.Connections;
using MongoTui.Domain.Primitives;

namespace MongoTui.Application.Services;

/// <summary>Builds a validated public profile and a separate secret for saving or testing.</summary>
public static class ConnectionProfilePreparation
{
    public static (ConnectionProfile Profile, string? Secret) Prepare(ConnectionProfileDraft draft,
        ConnectionProfile? original = null, string? originalSecret = null)
    {
        if (original is not null) (original, originalSecret) = ConnectionCredentials.Extract(original, originalSecret);
        var parsed = ConnectionUriParser.Parse(draft.Uri);
        var fields = draft.Auth ?? (parsed.Username is null && parsed.Password is null ? original?.Auth : null);
        var (auth, secret) = ConnectionCredentials.Merge(parsed, fields, draft.Password, original?.Auth, originalSecret);
        var profile = ConnectionProfile.Create(draft.Id ?? ConnectionId.New(), draft.Name, parsed.Uri, auth,
            draft.Tls ?? original?.Tls, draft.Ssh ?? original?.Ssh, original?.CreatedAt);
        return (profile, secret);
    }
}
