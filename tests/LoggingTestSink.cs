using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace MongoTui.Testing;

internal sealed class LoggingTestSink : ILoggerProvider, ISupportExternalScope
{
    private IExternalScopeProvider _scopes = new LoggerExternalScopeProvider();
    public ConcurrentQueue<TestLogEntry> Entries { get; } = new();
    public string Text => string.Join('\n', Entries.Select(entry => entry.Message + string.Join(' ', entry.Properties.Values) + entry.Exception));
    public ILoggerFactory CreateFactory() => LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Trace).AddProvider(this));
    public ILogger CreateLogger(string categoryName) => new CaptureLogger(this, categoryName);
    public void SetScopeProvider(IExternalScopeProvider scopeProvider) => _scopes = scopeProvider;
    public void Dispose() { }

    private sealed class CaptureLogger(LoggingTestSink owner, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => owner._scopes.Push(state);
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var properties = new Dictionary<string, object?>();
            owner._scopes.ForEachScope((scope, values) => Add(scope, values), properties);
            Add(state, properties);
            owner.Entries.Enqueue(new(category, logLevel, formatter(state, exception), properties, exception));
        }

        private static void Add(object? state, Dictionary<string, object?> properties)
        {
            if (state is IEnumerable<KeyValuePair<string, object?>> values)
                foreach (var (key, value) in values) properties[key] = value;
        }
    }
}

internal sealed record TestLogEntry(string Category, LogLevel Level, string Message,
    IReadOnlyDictionary<string, object?> Properties, Exception? Exception);
