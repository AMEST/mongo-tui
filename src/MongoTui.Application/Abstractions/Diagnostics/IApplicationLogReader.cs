using MongoTui.Application.Models;

namespace MongoTui.Application.Abstractions.Diagnostics;

/// <summary>Reads the application's retained log files without changing them.</summary>
public interface IApplicationLogReader
{
    /// <summary>Lists available files, newest first.</summary>
    Task<IReadOnlyList<ApplicationLogFile>> GetFilesAsync(CancellationToken cancellationToken);

    /// <summary>Reads a complete UTF-8 log file, or returns empty text if it has been removed by rotation.</summary>
    Task<string> ReadAsync(string fileName, CancellationToken cancellationToken);
}
