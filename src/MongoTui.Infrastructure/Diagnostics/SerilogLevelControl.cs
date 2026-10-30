using MongoTui.Application.Models;
using Serilog.Core;
using Serilog.Events;

namespace MongoTui.Infrastructure.Diagnostics;

/// <summary>Process-wide switches; preferences never require rebuilding the logger.</summary>
public sealed class SerilogLevelControl
{
    public const string EnvironmentVariable = "MONGOTUI_Serilog__MinimumLevel__Default";
    public LoggingLevelSwitch Application { get; } = new(LogEventLevel.Information);
    public LoggingLevelSwitch Framework { get; } = new(LogEventLevel.Warning);
    public ApplicationLogLevel? EnvironmentOverride { get; }
    public bool InvalidEnvironmentOverride { get; }

    public SerilogLevelControl(string? environmentLevel)
    {
        if (!string.IsNullOrWhiteSpace(environmentLevel))
        {
            if (Enum.TryParse<ApplicationLogLevel>(environmentLevel, ignoreCase: true, out var level) && Enum.IsDefined(level))
                EnvironmentOverride = level;
            else InvalidEnvironmentOverride = true;
        }
        Apply(ApplicationLogLevel.Information);
    }

    public LoggingSettings Apply(ApplicationLogLevel saved)
    {
        var effective = EnvironmentOverride ?? saved;
        Application.MinimumLevel = (LogEventLevel)effective;
        Framework.MinimumLevel = (LogEventLevel)Math.Max((int)effective, (int)LogEventLevel.Warning);
        return new(saved, effective, EnvironmentOverride is not null);
    }
}
