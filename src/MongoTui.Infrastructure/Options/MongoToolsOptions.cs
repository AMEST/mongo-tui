namespace MongoTui.Infrastructure.Options;

/// <summary>Configuration for locating the MongoDB Database Tools executables.</summary>
public sealed class MongoToolsOptions
{
    /// <summary>Optional directory that contains the tools; when empty the system PATH is used.</summary>
    public string? Directory { get; set; }
}
