using MongoTui.Application.Abstractions.Persistence;

namespace MongoTui.Infrastructure.FileSystem;

/// <summary>File-system backed <see cref="IQueryFileStore"/>.</summary>
public sealed class FileQueryFileStore : IQueryFileStore
{
    public Task<string> ReadAsync(string path, CancellationToken cancellationToken) =>
        File.ReadAllTextAsync(path, cancellationToken);

    public async Task WriteAsync(string path, string content, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(path, content, cancellationToken).ConfigureAwait(false);
    }
}
