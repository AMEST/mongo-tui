using System.Security.Cryptography;
using MongoTui.Application.Abstractions.Security;
using MongoTui.Application.Models;
using MongoTui.Domain.Exceptions;

namespace MongoTui.Infrastructure.Security;

/// <summary>Stores the KEK in a protected local file.</summary>
public sealed class FileKekSource : IKekSource
{
    internal const int KeySize = 32;

    private readonly IKekFileStore _store;

    public FileKekSource(IKekFileStore store) => _store = store;

    public SecurityMode Mode => SecurityMode.None;

    public bool IsAvailable => true;

    public async Task<bool> HasMaterialAsync(CancellationToken cancellationToken) =>
        await _store.TryReadAsync(cancellationToken) is not null;

    public async Task<byte[]?> TryGetKekAsync(CancellationToken cancellationToken)
    {
        var kek = await _store.TryReadAsync(cancellationToken);
        if (kek is null)
        {
            return null;
        }

        if (kek.Length != KeySize)
        {
            throw new DomainException("The KEK file has an invalid length.");
        }

        return kek;
    }

    public Task<byte[]> UnlockAsync(string? password, CancellationToken cancellationToken) =>
        throw new NotSupportedException("The 'none' security mode does not use a master password.");

    public async Task<byte[]> CreateAsync(string? password, CancellationToken cancellationToken)
    {
        var kek = RandomNumberGenerator.GetBytes(KeySize);
        await _store.WriteAsync(kek, cancellationToken);

        return kek;
    }

    public Task DeleteAsync(CancellationToken cancellationToken) =>
        _store.DeleteAsync(cancellationToken);
}
