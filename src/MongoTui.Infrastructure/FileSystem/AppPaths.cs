namespace MongoTui.Infrastructure.FileSystem;

/// <summary>Cross-platform locations of the application's data on disk.</summary>
public static class AppPaths
{
    private const string FolderName = ".mongotui";

    public static string RootDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), FolderName);

    public static string LogsDirectory => Path.Combine(RootDirectory, "logs");

    public static string LogFilePattern => Path.Combine(LogsDirectory, "app-.log");

    public static string DatabaseFile => Path.Combine(RootDirectory, "mongotui.db");
}
