using MongoTui.Application.Abstractions.Security;
using MongoTui.Infrastructure.FileSystem;

namespace MongoTui.Infrastructure.Security;

/// <summary>Persists the raw KEK in <c>~/.mongotui/kek.key</c> with user-only permissions.</summary>
public sealed class FileKekFileStore : IKekFileStore
{
    private const UnixFileMode OwnerOnly = UnixFileMode.UserRead | UnixFileMode.UserWrite;

    private readonly string _path;

    public FileKekFileStore()
        : this(AppPaths.KekFile)
    {
    }

    internal FileKekFileStore(string path) => _path = path;

    public string Path => _path;

    public async Task<byte[]?> TryReadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(Path))
        {
            return null;
        }

        return await File.ReadAllBytesAsync(Path, cancellationToken);
    }

    public async Task WriteAsync(byte[] kek, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(kek);

        var directory = System.IO.Path.GetDirectoryName(Path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporary = $"{Path}.{Guid.NewGuid():N}.tmp";
        try
        {
            await File.WriteAllBytesAsync(temporary, kek, cancellationToken);
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(temporary, OwnerOnly);
            }

            File.Move(temporary, Path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    public Task DeleteAsync(CancellationToken cancellationToken)
    {
        if (File.Exists(Path))
        {
            File.Delete(Path);
        }

        return Task.CompletedTask;
    }
}
