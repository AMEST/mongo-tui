using System.Security.Cryptography;
using MongoTui.Application.Abstractions.Persistence;
using MongoTui.Application.Abstractions.Security;
using MongoTui.Application.Models;
using MongoTui.Domain.Exceptions;

namespace MongoTui.Application.Services;

/// <summary>Inspects persisted settings and stored secrets to determine the security mode.</summary>
public sealed class SecurityModeDetector
{
    private readonly ISettingsStore _settings;
    private readonly IConnectionRepository _repository;
    private readonly IReadOnlyDictionary<SecurityMode, IKekSource> _sources;
    private readonly AiSettingsService _ai;

    public SecurityModeDetector(
        ISettingsStore settings,
        IConnectionRepository repository,
        IEnumerable<IKekSource> sources,
        AiSettingsService ai)
    {
        _settings = settings;
        _repository = repository;
        _sources = sources.ToDictionary(source => source.Mode);
        _ai = ai;
    }

    public async Task<SecurityModeDetection> DetectAsync(CancellationToken cancellationToken)
    {
        var stored = await _settings.GetAsync(SecurityModeService.ModeSettingKey, cancellationToken);
        var hasSecrets = await HasSecretsAsync(cancellationToken);

        if (!string.IsNullOrEmpty(stored))
        {
            return await DetectStoredModeAsync(stored, hasSecrets, cancellationToken);
        }

        if (_sources.TryGetValue(SecurityMode.Password, out var password))
        {
            try
            {
                if (await password.HasMaterialAsync(cancellationToken))
                {
                    return new SecurityModeDetection(SecurityState.Ready, SecurityMode.Password, null);
                }
            }
            catch (Exception exception) when (IsMaterialReadError(exception))
            {
                return new SecurityModeDetection(
                    SecurityState.Invalid, null, SecurityProblem.UnknownKeyMaterial);
            }
        }

        return hasSecrets
            ? new SecurityModeDetection(SecurityState.Invalid, null, SecurityProblem.UnknownKeyMaterial)
            : new SecurityModeDetection(SecurityState.FirstRun, null, null);
    }

    public async Task<bool> HasSecretsAsync(CancellationToken cancellationToken)
    {
        var connections = await _repository.GetAllAsync(cancellationToken);
        return connections.Any(connection => connection.Secret is not null)
            || await _ai.HasStoredApiKeyAsync(cancellationToken);
    }

    private async Task<SecurityModeDetection> DetectStoredModeAsync(
        string stored,
        bool hasSecrets,
        CancellationToken cancellationToken)
    {
        if (!SecurityModeValues.TryParse(stored, out var mode))
        {
            return new SecurityModeDetection(SecurityState.Invalid, null, SecurityProblem.UnknownKeyMaterial);
        }

        if (!_sources.TryGetValue(mode, out var source) || !source.IsAvailable)
        {
            return new SecurityModeDetection(
                SecurityState.Invalid,
                mode,
                mode == SecurityMode.Keyring
                    ? SecurityProblem.KeyringUnavailable
                    : SecurityProblem.UnknownKeyMaterial);
        }

        bool hasMaterial;
        try
        {
            hasMaterial = await source.HasMaterialAsync(cancellationToken);
        }
        catch (Exception exception) when (IsMaterialReadError(exception))
        {
            return new SecurityModeDetection(
                SecurityState.Invalid,
                mode,
                mode == SecurityMode.Keyring ? SecurityProblem.KeyringUnavailable : ProblemFor(mode));
        }

        if (hasMaterial)
        {
            return new SecurityModeDetection(SecurityState.Ready, mode, null);
        }

        return hasSecrets
            ? new SecurityModeDetection(SecurityState.Invalid, mode, ProblemFor(mode))
            : new SecurityModeDetection(SecurityState.Ready, mode, null);
    }

    private static bool IsMaterialReadError(Exception exception) =>
        exception is DomainException or IOException or UnauthorizedAccessException
            or FormatException or CryptographicException;

    private static SecurityProblem ProblemFor(SecurityMode mode) => mode switch
    {
        SecurityMode.Keyring => SecurityProblem.KeyringKeyMissing,
        SecurityMode.None => SecurityProblem.KekFileMissing,
        _ => SecurityProblem.PasswordMaterialMissing,
    };
}
