namespace MongoTui.Application.Abstractions.Persistence;

/// <summary>Key/value storage for application settings.</summary>
public interface ISettingsStore
{
    Task<string?> GetAsync(string key, CancellationToken cancellationToken);

    Task SetAsync(string key, string value, CancellationToken cancellationToken);

    Task DeleteAsync(string key, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<string, string>> GetAllAsync(CancellationToken cancellationToken);
}
