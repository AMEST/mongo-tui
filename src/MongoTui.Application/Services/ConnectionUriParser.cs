using System.Text;
using MongoTui.Application.Models;
using MongoTui.Domain.Connections;
using MongoTui.Domain.Exceptions;

namespace MongoTui.Application.Services;

/// <summary>Parses a MongoDB connection string into stored parts and credentials.</summary>
public static class ConnectionUriParser
{
    private const string PlainScheme = "mongodb://";
    private const string SrvScheme = "mongodb+srv://";

    public static ParsedConnectionUri Parse(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new DomainException("Connection string must not be empty.");
        }

        var value = connectionString.Trim();
        var scheme = SchemeOf(value);
        var rest = value[scheme.Length..];

        var query = string.Empty;
        var queryIndex = rest.IndexOf('?');
        if (queryIndex >= 0)
        {
            query = rest[(queryIndex + 1)..];
            rest = rest[..queryIndex];
        }

        var path = string.Empty;
        var slashIndex = rest.IndexOf('/');
        if (slashIndex >= 0)
        {
            path = rest[(slashIndex + 1)..];
            rest = rest[..slashIndex];
        }

        var authority = rest;
        string? username = null;
        string? password = null;
        var atIndex = authority.LastIndexOf('@');
        if (atIndex >= 0)
        {
            var userInfo = authority[..atIndex];
            authority = authority[(atIndex + 1)..];

            var colonIndex = userInfo.IndexOf(':');
            if (colonIndex >= 0)
            {
                username = Decode(userInfo[..colonIndex]);
                password = Decode(userInfo[(colonIndex + 1)..]);
            }
            else
            {
                username = Decode(userInfo);
            }
        }

        var uriBuilder = new StringBuilder(scheme).Append(authority);
        if (slashIndex >= 0)
        {
            uriBuilder.Append('/').Append(path);
        }

        if (query.Length > 0)
        {
            uriBuilder.Append('?').Append(query);
        }

        var parameters = ParseQuery(query);
        var authDatabase = ResolveAuthDatabase(parameters, path);
        var authKind = ResolveAuthKind(parameters, username);

        return new ParsedConnectionUri
        {
            Uri = uriBuilder.ToString(),
            Host = HostOf(authority),
            Username = username,
            Password = password,
            Database = path.Length > 0 ? Decode(path) : null,
            AuthDatabase = authDatabase,
            AuthKind = authKind,
        };
    }

    private static string SchemeOf(string value)
    {
        if (value.StartsWith(SrvScheme, StringComparison.OrdinalIgnoreCase))
        {
            return SrvScheme;
        }

        if (value.StartsWith(PlainScheme, StringComparison.OrdinalIgnoreCase))
        {
            return PlainScheme;
        }

        throw new DomainException("Connection string must use mongodb:// or mongodb+srv://.");
    }

    private static string HostOf(string authority)
    {
        var end = authority.IndexOfAny([',', ':']);
        return end >= 0 ? authority[..end] : authority;
    }

    private static string? ResolveAuthDatabase(IReadOnlyDictionary<string, string> parameters, string path)
    {
        if (parameters.TryGetValue("authSource", out var authSource) && authSource.Length > 0)
        {
            return authSource;
        }

        return path.Length > 0 ? path : null;
    }

    private static AuthKind ResolveAuthKind(IReadOnlyDictionary<string, string> parameters, string? username)
    {
        if (parameters.TryGetValue("authMechanism", out var mechanism))
        {
            return mechanism.ToUpperInvariant() switch
            {
                "SCRAM-SHA-1" => AuthKind.ScramSha1,
                "SCRAM-SHA-256" => AuthKind.ScramSha256,
                "MONGODB-X509" => AuthKind.X509,
                "PLAIN" => AuthKind.Plain,
                _ => string.IsNullOrEmpty(username) ? AuthKind.None : AuthKind.ScramSha256,
            };
        }

        return string.IsNullOrEmpty(username) ? AuthKind.None : AuthKind.ScramSha256;
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (query.Length == 0)
        {
            return result;
        }

        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = pair.IndexOf('=');
            if (separator < 0)
            {
                result[Decode(pair)] = string.Empty;
                continue;
            }

            result[Decode(pair[..separator])] = Decode(pair[(separator + 1)..]);
        }

        return result;
    }

    private static string Decode(string value) => Uri.UnescapeDataString(value);
}
