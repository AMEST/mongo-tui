namespace MongoTui.Application.Models;

/// <summary>Supported verbosity levels, ordered from most to least detailed.</summary>
public enum ApplicationLogLevel { Verbose, Debug, Information, Warning, Error, Fatal }

public sealed record LoggingSettings(ApplicationLogLevel SavedLevel, ApplicationLogLevel EffectiveLevel, bool IsOverridden);
