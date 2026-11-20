using MongoTui.Infrastructure.Options;

namespace MongoTui.Infrastructure.Mongo.Cli;

/// <summary>Resolves the full path of MongoDB Database Tools executables.</summary>
public sealed class MongoToolsLocator
{
    private readonly string? _configuredDirectory;
    private readonly string _bundledDirectory;

    public MongoToolsLocator(MongoToolsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _configuredDirectory = options.Directory;
        _bundledDirectory = options.BundledDirectory;
    }

    /// <summary>Returns the best-known path for <paramref name="toolName"/>, falling back to its bare name.</summary>
    public string Resolve(string toolName)
    {
        var executable = ExecutableName(toolName);

        return FindInDirectory(_configuredDirectory, executable)
            ?? (OperatingSystem.IsWindows() ? FindInDirectory(_bundledDirectory, executable) : null)
            ?? FindOnPath(executable)
            ?? toolName;
    }

    private static string ExecutableName(string toolName) =>
        OperatingSystem.IsWindows() ? toolName + ".exe" : toolName;

    private static string? FindInDirectory(string? directory, string executable)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            return null;
        }

        var candidate = Path.Combine(directory, executable);
        return File.Exists(candidate) ? candidate : null;
    }

    private static string? FindOnPath(string executable)
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        foreach (var directory in path.Split(Path.PathSeparator))
        {
            var candidate = FindInDirectory(directory, executable);
            if (candidate is not null)
            {
                return candidate;
            }
        }

        return null;
    }
}
