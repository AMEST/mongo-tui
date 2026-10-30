using System.Collections.Concurrent;
using MongoTui.Application.Abstractions.Persistence;

namespace MongoTui.Testing;

internal sealed class MemorySettingsStore : ISettingsStore
{
    private readonly ConcurrentDictionary<string, string> _values = new(StringComparer.Ordinal);
    public Task<string?> GetAsync(string key, CancellationToken cancellationToken) =>
        Task.FromResult(_values.TryGetValue(key, out var value) ? value : null);
    public Task SetAsync(string key, string value, CancellationToken cancellationToken)
    { cancellationToken.ThrowIfCancellationRequested(); _values[key] = value; return Task.CompletedTask; }
    public Task DeleteAsync(string key, CancellationToken cancellationToken)
    { _values.TryRemove(key, out _); return Task.CompletedTask; }
    public Task<IReadOnlyDictionary<string, string>> GetAllAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>(_values));
}
