using MongoTui.Application.Models;

namespace MongoTui.Application.Abstractions.Diagnostics;

/// <summary>Persists logging preferences and applies them to the running process.</summary>
public interface ILoggingSettings
{
    Task<LoggingSettings> GetAsync(CancellationToken cancellationToken);
    Task<LoggingSettings> SaveAsync(ApplicationLogLevel level, CancellationToken cancellationToken);
}
