using System.Security.Cryptography;
using MongoTui.Application.Abstractions.Security;
using MongoTui.Application.Models;
using MongoTui.Domain.Exceptions;

namespace MongoTui.Infrastructure.Security;

/// <summary>Stores the KEK in the OS keyring.</summary>
public sealed class KeyringKekSource : IKekSource
{
    internal const string KekKey = "kek";
    internal const int KeySize = 32;

    private readonly IKeyring _keyring;

    public KeyringKekSource(IKeyring keyring) => _keyring = keyring;

    public SecurityMode Mode => SecurityMode.Keyring;

    public bool IsAvailable => _keyring.IsAvailable;

    public async Task<bool> HasMaterialAsync(CancellationToken cancellationToken)
    {
        if (!IsAvailable)
        {
            return false;
        }

        return await _keyring.GetAsync(KekKey, cancellationToken) is not null;
    }

    public async Task<byte[]?> TryGetKekAsync(CancellationToken cancellationToken)
    {
        var existing = await _keyring.GetAsync(KekKey, cancellationToken);
        if (existing is null)
        {
            return null;
        }

        if (existing.Length != KeySize)
        {
            throw new DomainException("The keyring KEK has an invalid length.");
        }

        return existing;
    }

    public Task<byte[]> UnlockAsync(string? password, CancellationToken cancellationToken) =>
        throw new NotSupportedException("The keyring security mode does not use a master password.");

    public async Task<byte[]> CreateAsync(string? password, CancellationToken cancellationToken)
    {
        var kek = RandomNumberGenerator.GetBytes(KeySize);
        await _keyring.SetAsync(KekKey, kek, cancellationToken);

        return kek;
    }

    public Task DeleteAsync(CancellationToken cancellationToken) =>
        _keyring.DeleteAsync(KekKey, cancellationToken);
}
