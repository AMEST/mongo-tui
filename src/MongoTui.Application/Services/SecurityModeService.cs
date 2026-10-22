using System.Security.Cryptography;
using MongoTui.Application.Abstractions.Persistence;
using MongoTui.Application.Abstractions.Security;
using MongoTui.Application.Models;
using MongoTui.Domain.Exceptions;

namespace MongoTui.Application.Services;

/// <summary>Detects, initializes and switches the security mode protecting stored secrets.</summary>
public sealed class SecurityModeService
{
    internal const string ModeSettingKey = "security.kek_mode";

    private readonly ISettingsStore _settings;
    private readonly IReadOnlyDictionary<SecurityMode, IKekSource> _sources;
    private readonly SecurityModeDetector _detector;
    private readonly SecretKeyService _secrets;
    private readonly ConnectionService _connections;
    private readonly AiSettingsService _ai;
    private readonly IStorageTransaction _transaction;

    public SecurityModeService(
        ISettingsStore settings,
        IEnumerable<IKekSource> sources,
        SecretKeyService secrets,
        ConnectionService connections,
        AiSettingsService ai,
        IStorageTransaction transaction,
        SecurityModeDetector detector)
    {
        _settings = settings;
        _sources = sources.ToDictionary(source => source.Mode);
        _detector = detector;
        _secrets = secrets;
        _connections = connections;
        _ai = ai;
        _transaction = transaction;
    }

    public SecurityMode? CurrentMode { get; private set; }

    public bool IsUnlocked => _secrets.IsUnlocked;

    public IReadOnlyList<SecurityMode> AvailableModes =>
        Enum.GetValues<SecurityMode>()
            .Where(mode => _sources.TryGetValue(mode, out var source) && source.IsAvailable)
            .ToList();

    public async Task<SecurityModeDetection> DetectAsync(CancellationToken cancellationToken)
    {
        var detection = await _detector.DetectAsync(cancellationToken);
        CurrentMode = detection.Mode;
        return detection;
    }

    public Task InitializeAsync(SecurityMode mode, string? password, CancellationToken cancellationToken) =>
        _secrets.RunExclusiveAsync(async token =>
        {
            if (CurrentMode is not null)
            {
                throw new DomainException("The security mode is already initialized.");
            }

            SecurityModeValues.ValidatePassword(mode, password);
            var source = SourceFor(mode);
            if (!source.IsAvailable)
            {
                throw new DomainException($"Security mode '{mode}' is not available.");
            }

            var kek = await source.CreateAsync(SecurityModeValues.NeedsPassword(mode) ? password : null, token);
            try
            {
                await _settings.SetAsync(ModeSettingKey, SecurityModeValues.ToSettingValue(mode), token);
            }
            catch
            {
                await KekSourceCleanup.DeleteAsync(source, token);
                throw;
            }

            _secrets.SetKek(kek);
            CurrentMode = mode;
        }, cancellationToken);

    public Task<AutoUnlockResult> TryAutoUnlockAsync(CancellationToken cancellationToken) =>
        _secrets.RunExclusiveAsync(AutoUnlockCoreAsync, cancellationToken);

    private async Task<AutoUnlockResult> AutoUnlockCoreAsync(CancellationToken cancellationToken)
    {
        if (_secrets.IsUnlocked) return AutoUnlockResult.Unlocked;
        if (CurrentMode is null) await DetectAsync(cancellationToken);
        if (CurrentMode is not { } mode) return AutoUnlockResult.KeyMaterialUnavailable;
        if (mode == SecurityMode.Password) return AutoUnlockResult.PasswordRequired;

        if (!_sources.TryGetValue(mode, out var source) || !source.IsAvailable)
        {
            return AutoUnlockResult.KeyMaterialUnavailable;
        }

        return await UnlockFromSourceAsync(source, cancellationToken);
    }

    private async Task<AutoUnlockResult> UnlockFromSourceAsync(IKekSource source, CancellationToken cancellationToken)
    {
        try
        {
            var kek = await source.TryGetKekAsync(cancellationToken);
            if (kek is null)
            {
                if (await HasSecretsAsync(cancellationToken))
                {
                    return AutoUnlockResult.KeyMaterialUnavailable;
                }

                kek = await source.CreateAsync(null, cancellationToken);
            }

            await _connections.ValidateKekAsync(kek, cancellationToken);
            await _ai.ValidateKekAsync(kek, cancellationToken);
            _secrets.SetKek(kek);
            await _connections.MigrateCredentialsAsync(cancellationToken);
            return AutoUnlockResult.Unlocked;
        }
        catch (Exception exception) when (exception is DomainException or CryptographicException)
        {
            _secrets.Lock();
            return AutoUnlockResult.KeyMaterialUnavailable;
        }
        catch
        {
            _secrets.Lock();
            throw;
        }
    }

    public Task UnlockWithPasswordAsync(string password, CancellationToken cancellationToken) =>
        _secrets.RunExclusiveAsync(async token =>
        {
            if (CurrentMode is null)
            {
                await DetectAsync(token);
            }

            if (CurrentMode != SecurityMode.Password)
            {
                throw new DomainException("Secrets are not protected by a master password.");
            }

            try
            {
                var kek = await SourceFor(SecurityMode.Password).UnlockAsync(password, token);
                _secrets.SetKek(kek);
                await _connections.MigrateCredentialsAsync(token);
                await EnsureLegacyMarkerAsync(token);
            }
            catch
            {
                _secrets.Lock();
                throw;
            }
        }, cancellationToken);

    public Task ChangePasswordAsync(string newPassword, CancellationToken cancellationToken) =>
        _secrets.RunExclusiveAsync(async token =>
        {
            if (CurrentMode != SecurityMode.Password)
            {
                throw new DomainException("Master password protection is not enabled.");
            }

            await SwitchCoreAsync(SecurityMode.Password, newPassword, token);
        }, cancellationToken);

    public Task SwitchModeAsync(SecurityMode mode, string? password, CancellationToken cancellationToken) =>
        _secrets.RunExclusiveAsync(token => SwitchCoreAsync(mode, password, token), cancellationToken);

    public Task ResetAsync(CancellationToken cancellationToken) =>
        _secrets.RunExclusiveAsync(async token =>
        {
            await _connections.ClearSecretsAsync(token);
            await _ai.ClearApiKeyAsync(token);

            foreach (var source in _sources.Values)
            {
                await KekSourceCleanup.DeleteAsync(source, token);
            }

            await _settings.DeleteAsync(ModeSettingKey, token);
            _secrets.Lock();
            CurrentMode = null;
        }, cancellationToken);

    public Task<bool> HasSecretsAsync(CancellationToken cancellationToken) =>
        _detector.HasSecretsAsync(cancellationToken);

    private async Task SwitchCoreAsync(SecurityMode mode, string? password, CancellationToken cancellationToken)
    {
        if (CurrentMode is not { } previous)
        {
            throw new DomainException("The security mode is not initialized.");
        }

        if (previous == mode && mode != SecurityMode.Password)
        {
            return;
        }

        if (!_secrets.IsUnlocked)
        {
            throw new DomainException("Secrets are locked; unlock them before changing the security mode.");
        }

        SecurityModeValues.ValidatePassword(mode, password);
        var source = SourceFor(mode);
        if (!source.IsAvailable)
        {
            throw new DomainException($"Security mode '{mode}' is not available.");
        }

        var rotation = mode == SecurityMode.Password
            ? await RotateToPasswordAsync(source, password, cancellationToken)
            : await RotateToExternalKeyAsync(source, mode, cancellationToken);

        rotation.Commit();
        if (previous != mode)
        {
            await KekSourceCleanup.DeleteAsync(SourceFor(previous), cancellationToken);
        }

        CurrentMode = mode;
    }

    private async Task<KekRotation> RotateToPasswordAsync(
        IKekSource source,
        string? password,
        CancellationToken cancellationToken)
    {
        KekRotation? rotation = null;
        await _transaction.ExecuteAsync(async token =>
        {
            var newKek = await source.CreateAsync(password, token);
            rotation = _secrets.BeginRotation(newKek);
            await _connections.ReEncryptSecretsAsync(rotation, token);
            await _ai.ReEncryptAsync(rotation, token);
            await _settings.SetAsync(ModeSettingKey, SecurityModeValues.ToSettingValue(SecurityMode.Password), token);
        }, cancellationToken);

        return rotation!;
    }

    private async Task<KekRotation> RotateToExternalKeyAsync(
        IKekSource source,
        SecurityMode mode,
        CancellationToken cancellationToken)
    {
        var newKek = await source.CreateAsync(null, cancellationToken);
        KekRotation? rotation = null;
        try
        {
            rotation = _secrets.BeginRotation(newKek);
            await _transaction.ExecuteAsync(async token =>
            {
                await _connections.ReEncryptSecretsAsync(rotation, token);
                await _ai.ReEncryptAsync(rotation, token);
                await _settings.SetAsync(ModeSettingKey, SecurityModeValues.ToSettingValue(mode), token);
            }, cancellationToken);
        }
        catch
        {
            await KekSourceCleanup.DeleteAsync(source, cancellationToken);
            throw;
        }

        return rotation!;
    }

    private async Task EnsureLegacyMarkerAsync(CancellationToken cancellationToken)
    {
        if (await _settings.GetAsync(ModeSettingKey, cancellationToken) is null)
        {
            await _settings.SetAsync(ModeSettingKey, SecurityModeValues.ToSettingValue(SecurityMode.Password), cancellationToken);
        }
    }

    private IKekSource SourceFor(SecurityMode mode) =>
        _sources.TryGetValue(mode, out var source)
            ? source
            : throw new DomainException($"Security mode '{mode}' is not supported.");
}
