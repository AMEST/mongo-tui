using System.Text;
using MongoTui.Application.Abstractions.Diagnostics;
using MongoTui.Application.Models;

namespace MongoTui.Infrastructure.FileSystem;

/// <summary>Reads Serilog files while the application can continue writing or rotating them.</summary>
public sealed class FileApplicationLogReader(string directory) : IApplicationLogReader
{
    public Task<IReadOnlyList<ApplicationLogFile>> GetFilesAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var files = new List<ApplicationLogFile>();
        try
        {
            foreach (var path in Directory.EnumerateFiles(directory, "app-*.log", SearchOption.TopDirectoryOnly))
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var info = new FileInfo(path);
                    if ((info.Attributes & FileAttributes.ReparsePoint) == 0)
                        files.Add(new ApplicationLogFile(info.Name, info.Length, info.LastWriteTimeUtc));
                }
                catch (FileNotFoundException) { }
            }
        }
        catch (DirectoryNotFoundException) { }

        return Task.FromResult<IReadOnlyList<ApplicationLogFile>>(
            files.OrderByDescending(file => file.Name, StringComparer.Ordinal).ToArray());
    }

    public async Task<string> ReadAsync(string fileName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrEmpty(fileName) || fileName.IndexOfAny(['/', '\\']) >= 0
            || !fileName.StartsWith("app-", StringComparison.Ordinal) || !fileName.EndsWith(".log", StringComparison.Ordinal))
            throw new ArgumentException("Only application log file names are allowed.", nameof(fileName));

        try
        {
            var path = Path.Combine(directory, fileName);
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new ArgumentException("Only regular application log files can be read.", nameof(fileName));

            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            return await reader.ReadToEndAsync(cancellationToken);
        }
        catch (FileNotFoundException) { return string.Empty; }
        catch (DirectoryNotFoundException) { return string.Empty; }
    }
}
