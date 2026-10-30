using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MongoTui.Application.Abstractions.Persistence;

namespace MongoTui.Web.Services;

/// <summary>A persisted security fingerprint revokes sessions after key changes in any process.</summary>
public sealed class WebSecurityStamp(ISettingsStore settings)
{
    public async Task<WebSecurityState> ReadAsync(CancellationToken cancellationToken)
    {
        var values = await settings.GetAllAsync(cancellationToken).ConfigureAwait(false);
        var security = values.Where(pair => pair.Key.StartsWith("security.", StringComparison.Ordinal))
            .OrderBy(pair => pair.Key, StringComparer.Ordinal).ToArray();
        var revision = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(security))));
        var password = values.GetValueOrDefault("security.kek_mode") == "password"
            && !string.IsNullOrEmpty(values.GetValueOrDefault("security.kek_salt"))
            && !string.IsNullOrEmpty(values.GetValueOrDefault("security.kek_check"));
        return new WebSecurityState(revision, password);
    }
}

public sealed record WebSecurityState(string Revision, bool PasswordEnabled);
