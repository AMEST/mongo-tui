using MongoTui.Domain.Connections;

namespace MongoTui.Application.Services;

/// <summary>Builds a connection string for a specific MongoDB user from a saved connection profile.</summary>
public static class UserConnectionStringBuilder
{
    public static string Build(ConnectionProfile profile, string username, string password, string authDatabase)
    {
        var uri = profile.Uri;
        if (string.IsNullOrWhiteSpace(uri))
        {
            throw new ArgumentException("Profile URI must not be empty.", nameof(profile));
        }

        var schemeEnd = uri.IndexOf("://", StringComparison.Ordinal);
        if (schemeEnd < 0)
        {
            return uri;
        }

        schemeEnd += 3;
        var rest = uri[schemeEnd..];

        var atIndex = rest.LastIndexOf('@');
        if (atIndex >= 0)
        {
            rest = rest[(atIndex + 1)..];
        }

        var pathAndAuth = rest;
        var query = string.Empty;
        var queryIndex = rest.IndexOf('?');
        if (queryIndex >= 0)
        {
            pathAndAuth = rest[..queryIndex];
            query = rest[(queryIndex + 1)..];
        }

        var cleanedQuery = CleanQuery(query);
        if (!string.IsNullOrWhiteSpace(authDatabase))
        {
            cleanedQuery = string.IsNullOrEmpty(cleanedQuery)
                ? $"authSource={Uri.EscapeDataString(authDatabase)}"
                : $"{cleanedQuery}&authSource={Uri.EscapeDataString(authDatabase)}";
        }

        var credentials = $"{Uri.EscapeDataString(username)}:{Uri.EscapeDataString(password)}@";
        var result = uri[..schemeEnd] + credentials + pathAndAuth;

        return string.IsNullOrEmpty(cleanedQuery) ? result : $"{result}?{cleanedQuery}";
    }

    private static string CleanQuery(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return string.Empty;
        }

        var kept = new List<string>();
        foreach (var part in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = part.IndexOf('=');
            var key = separator >= 0 ? part[..separator] : part;
            var decodedKey = Uri.UnescapeDataString(key);
            if (IsRemovedParameter(decodedKey))
            {
                continue;
            }

            kept.Add(part);
        }

        return string.Join("&", kept);
    }

    private static bool IsRemovedParameter(string key) =>
        string.Equals(key, "authSource", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(key, "authMechanism", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(key, "proxyHost", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(key, "proxyPort", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(key, "proxyUsername", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(key, "proxyPassword", StringComparison.OrdinalIgnoreCase);
}
