using System.Collections.Concurrent;
using System.Diagnostics;
using MongoTui.Application.Abstractions.Mongo;
using MongoTui.Application.Abstractions.Persistence;
using MongoTui.Application.Models;
using MongoTui.Domain.Exceptions;
using MongoTui.Domain.Queries;

namespace MongoTui.Application.Services;

/// <summary>
/// Executes tab scripts through lazily created <c>mongosh</c> sessions and records history.
/// One session is reused per tab for as long as the tab is open.
/// </summary>
public sealed class QueryService : IAsyncDisposable
{
    private readonly IMongoShellSessionFactory _sessionFactory;
    private readonly IQueryHistoryRepository _history;
    private readonly ConcurrentDictionary<Guid, IMongoShellSession> _sessions = new();

    public QueryService(IMongoShellSessionFactory sessionFactory, IQueryHistoryRepository history)
    {
        _sessionFactory = sessionFactory;
        _history = history;
    }

    public async Task<QueryResult> ExecuteAsync(QueryExecutionContext context, CancellationToken cancellationToken)
    {
        var session = await GetOrCreateSessionAsync(context.TabId, context.Target, cancellationToken);

        var stopwatch = Stopwatch.StartNew();
        var success = false;

        try
        {
            var result = await session.EvaluateAsync(context.Script, cancellationToken);
            success = true;

            return result with { Elapsed = stopwatch.Elapsed };
        }
        catch (Exception exception) when (exception is not DomainException and not OperationCanceledException)
        {
            throw new QueryExecutionException("Query execution failed.", exception);
        }
        finally
        {
            stopwatch.Stop();
            await RecordHistoryAsync(context, success, stopwatch.Elapsed, cancellationToken);
        }
    }

    public async Task CloseTabAsync(Guid tabId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_sessions.TryRemove(tabId, out var session))
        {
            await session.DisposeAsync();
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var session in _sessions.Values)
        {
            await session.DisposeAsync();
        }

        _sessions.Clear();
    }

    private async Task<IMongoShellSession> GetOrCreateSessionAsync(
        Guid tabId,
        MongoConnectionTarget target,
        CancellationToken cancellationToken)
    {
        if (_sessions.TryGetValue(tabId, out var existing) && existing.IsRunning)
        {
            return existing;
        }

        var session = await _sessionFactory.CreateAsync(target, cancellationToken);
        _sessions[tabId] = session;

        return session;
    }

    private Task RecordHistoryAsync(
        QueryExecutionContext context,
        bool success,
        TimeSpan elapsed,
        CancellationToken cancellationToken) =>
        _history.AddAsync(
            new QueryHistoryEntry
            {
                ConnectionId = context.ConnectionId,
                Database = context.Database,
                Collection = context.Collection,
                Script = context.Script,
                RanAt = DateTimeOffset.UtcNow,
                Success = success,
                ElapsedMs = (long)elapsed.TotalMilliseconds,
            },
            cancellationToken);
}
