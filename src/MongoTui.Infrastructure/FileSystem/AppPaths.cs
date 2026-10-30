namespace MongoTui.Infrastructure.FileSystem;

/// <summary>Cross-platform locations of the application's data on disk.</summary>
public static class AppPaths
{
    private const string FolderName = ".mongotui";
    public const string DataDirectoryEnvironmentVariable = "MONGOTUI_DATA_DIR";

    private static string RootDirectory =>
        Environment.GetEnvironmentVariable(DataDirectoryEnvironmentVariable) is { Length: > 0 } directory
            ? Path.GetFullPath(directory)
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), FolderName);

    public static string LogsDirectory => Path.Combine(RootDirectory, "logs");

    public static string LogFilePattern => Path.Combine(LogsDirectory, "app-.log");

    public static string DatabaseFile => Path.Combine(RootDirectory, "mongotui.db");

    public static string KekFile => Path.Combine(RootDirectory, "kek.key");
}
