using MongoTui.Infrastructure.Options;

namespace MongoTui.Infrastructure.Mongo.Cli;

/// <summary>Resolves the full path of MongoDB Database Tools executables.</summary>
public sealed class MongoToolsLocator
{
    private readonly string? _configuredDirectory;

    public MongoToolsLocator(MongoToolsOptions options) => _configuredDirectory = options.Directory;

    /// <summary>Returns the best-known path for <paramref name="toolName"/>, falling back to its bare name.</summary>
    public string Resolve(string toolName)
    {
        var executable = ExecutableName(toolName);

        if (!string.IsNullOrWhiteSpace(_configuredDirectory))
        {
            var candidate = Path.Combine(_configuredDirectory, executable);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return FindOnPath(executable) ?? toolName;
    }

    private static string ExecutableName(string toolName) =>
        OperatingSystem.IsWindows() ? toolName + ".exe" : toolName;

    private static string? FindOnPath(string executable)
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        foreach (var directory in path.Split(Path.PathSeparator))
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                continue;
            }

            var candidate = Path.Combine(directory, executable);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }
}
