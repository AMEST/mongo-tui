namespace MongoTui.Infrastructure.Options;

/// <summary>Configuration for locating the MongoDB Database Tools executables.</summary>
public sealed class MongoToolsOptions
{
    /// <summary>Optional directory that contains the tools and takes precedence over bundled tools and PATH.</summary>
    public string? Directory { get; set; }

    /// <summary>Directory with tools shipped next to the application.</summary>
    public string BundledDirectory { get; set; } = Path.Combine(AppContext.BaseDirectory, "thirdparty");
}
