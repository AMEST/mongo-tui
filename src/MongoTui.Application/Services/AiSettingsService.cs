using MongoTui.Application.Abstractions.Persistence;
using MongoTui.Application.Models;

namespace MongoTui.Application.Services;

/// <summary>
/// Stores and resolves the AI assistant configuration. The API key is persisted encrypted with
/// the master key, while the base URL and model name are stored as plain settings.
/// </summary>
public sealed class AiSettingsService
{
    private const string BaseUrlKey = "ai.base_url";
    private const string ModelKey = "ai.model";
    private const string ApiKeyKey = "ai.api_key";

    private readonly ISettingsStore _settings;
    private readonly SecretKeyService _secrets;

    public AiSettingsService(ISettingsStore settings, SecretKeyService secrets)
    {
        _settings = settings;
        _secrets = secrets;
    }

    public Task<AiConfiguration> GetAsync(CancellationToken cancellationToken) =>
        _secrets.RunExclusiveAsync(GetCoreAsync, cancellationToken);

    private async Task<AiConfiguration> GetCoreAsync(CancellationToken cancellationToken)
    {
        var baseUrl = await _settings.GetAsync(BaseUrlKey, cancellationToken) ?? string.Empty;
        var model = await _settings.GetAsync(ModelKey, cancellationToken) ?? string.Empty;
        var apiKey = await GetApiKeyAsync(cancellationToken);

        return new AiConfiguration(baseUrl, model, apiKey);
    }

    public async Task<bool> IsConfiguredAsync(CancellationToken cancellationToken) =>
        (await GetAsync(cancellationToken)).IsConfigured;

    /// <summary>
    /// Saves the configuration. Pass a non-null <paramref name="apiKey"/> to replace the stored key
    /// (empty string clears it); pass <see langword="null"/> to leave it unchanged.
    /// </summary>
    public Task SaveAsync(
        string baseUrl,
        string model,
        string? apiKey,
        CancellationToken cancellationToken) =>
        _secrets.RunExclusiveAsync(token => SaveCoreAsync(baseUrl, model, apiKey, token), cancellationToken);

    private async Task SaveCoreAsync(
        string baseUrl,
        string model,
        string? apiKey,
        CancellationToken cancellationToken)
    {
        await _settings.SetAsync(BaseUrlKey, baseUrl.Trim(), cancellationToken);
        await _settings.SetAsync(ModelKey, model.Trim(), cancellationToken);

        if (apiKey is null)
        {
            return;
        }

        await _settings.SetAsync(
            ApiKeyKey,
            apiKey.Length == 0 ? string.Empty : Pack(_secrets.Protect(apiKey)),
            cancellationToken);
    }

    /// <summary>Re-encrypts the stored API key with the rotated KEK.</summary>
    public async Task ReEncryptAsync(MasterPasswordRotation rotation, CancellationToken cancellationToken)
    {
        var stored = await _settings.GetAsync(ApiKeyKey, cancellationToken);

        if (string.IsNullOrEmpty(stored))
        {
            return;
        }

        await _settings.SetAsync(ApiKeyKey, Pack(rotation.ReEncrypt(Unpack(stored))), cancellationToken);
    }

    private async Task<string?> GetApiKeyAsync(CancellationToken cancellationToken)
    {
        if (!_secrets.IsUnlocked)
        {
            return null;
        }

        var stored = await _settings.GetAsync(ApiKeyKey, cancellationToken);

        return string.IsNullOrEmpty(stored) ? null : _secrets.Unprotect(Unpack(stored));
    }

    private static string Pack(EncryptedSecret secret)
    {
        var blob = new byte[1 + secret.Nonce.Length + secret.CipherText.Length];
        blob[0] = (byte)secret.Nonce.Length;
        secret.Nonce.CopyTo(blob.AsSpan(1));
        secret.CipherText.CopyTo(blob.AsSpan(1 + secret.Nonce.Length));

        return Convert.ToBase64String(blob);
    }

    private static EncryptedSecret Unpack(string stored)
    {
        var blob = Convert.FromBase64String(stored);
        var nonceLength = blob[0];

        return new EncryptedSecret(
            blob.AsSpan(1, nonceLength).ToArray(),
            blob.AsSpan(1 + nonceLength).ToArray());
    }
}
