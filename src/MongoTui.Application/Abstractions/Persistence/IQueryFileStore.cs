namespace MongoTui.Application.Abstractions.Persistence;

/// <summary>Reads and writes query script files on the local file system.</summary>
public interface IQueryFileStore
{
    Task<string> ReadAsync(string path, CancellationToken cancellationToken);

    Task WriteAsync(string path, string content, CancellationToken cancellationToken);
}
