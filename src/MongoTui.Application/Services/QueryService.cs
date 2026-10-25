using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using MongoTui.Application.Diagnostics;
using MongoTui.Application.Abstractions.Mongo;
using MongoTui.Application.Abstractions.Persistence;
using MongoTui.Application.Models;
using MongoTui.Domain.Collections;
using MongoTui.Domain.Exceptions;
using MongoTui.Domain.Queries;

namespace MongoTui.Application.Services;

/// <summary>
/// Executes shell scripts and collection queries with the shared query deadline.
/// Shell sessions are reused per tab and script executions are recorded in history.
/// </summary>
public sealed class QueryService : IAsyncDisposable
{
    private readonly IMongoShellSessionFactory _sessionFactory;
    private readonly IQueryHistoryRepository _history;
    private readonly IMongoGateway _gateway;
    private readonly SettingsService _settings;
    private readonly ILogger<QueryService> _logger;
    private readonly ConcurrentDictionary<Guid, IMongoShellSession> _sessions = new();

    public QueryService(IMongoShellSessionFactory sessionFactory, IQueryHistoryRepository history,
        IMongoGateway gateway, SettingsService settings, ILogger<QueryService> logger)
    {
        _sessionFactory = sessionFactory;
        _history = history;
        _gateway = gateway;
        _settings = settings;
        _logger = logger;
    }

    public async Task<QueryResult> ExecuteAsync(QueryExecutionContext context, CancellationToken cancellationToken)
    {
        using var scope = _logger.BeginScope(new Dictionary<string, object?>
        {
            [LogPropertyNames.ConnectionId] = context.ConnectionId.Value, [LogPropertyNames.TabId] = context.TabId,
            [LogPropertyNames.Database] = context.Database, [LogPropertyNames.Collection] = context.Collection,
        });
        _logger.LogDebug("Executing shell query ({ScriptLength} characters)", context.Script.Length);
        return await ExecuteWithTimeoutAsync((_, token) => ExecuteScriptAsync(context, cancellationToken, token),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Uses the same configured deadline as shell queries, including the driver's server timeout.</summary>
    public async Task<QueryResult> FindAsync(MongoConnectionTarget target, CollectionRef collection,
        string filterJson, QueryExecutionOptions options, CancellationToken cancellationToken)
    {
        using var scope = _logger.BeginScope(new Dictionary<string, object?>
        {
            [LogPropertyNames.Database] = collection.Database.Value, [LogPropertyNames.Collection] = collection.Collection.Value,
        });
        _logger.LogDebug("Reading documents (skip {Skip}, limit {Limit})", options.Skip, options.Limit);
        return await ExecuteWithTimeoutAsync((timeout, token) => _gateway.FindAsync(target, collection, filterJson,
            options with { Timeout = timeout }, token), cancellationToken).ConfigureAwait(false);
    }

    private async Task<QueryResult> ExecuteWithTimeoutAsync(
        Func<TimeSpan?, CancellationToken, Task<QueryResult>> execute, CancellationToken cancellationToken)
    {
        var seconds = 0;
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var timer = Stopwatch.StartNew();
        try
        {
            seconds = await _settings.GetQueryTimeoutSecondsAsync(cancellationToken).ConfigureAwait(false);
            var timeout = seconds == 0 ? (TimeSpan?)null : TimeSpan.FromSeconds(seconds);
            if (timeout is not null) operation.CancelAfter(timeout.Value);
            var result = await execute(timeout, timeout is null ? cancellationToken : operation.Token).ConfigureAwait(false);
            _logger.LogInformation("Query completed in {ElapsedMs:0.0} ms ({ResultCount} results)", timer.Elapsed.TotalMilliseconds, result.Documents.Count);
            return result;
        }
        catch (OperationCanceledException exception)
            when (!cancellationToken.IsCancellationRequested && operation.IsCancellationRequested)
        {
            _logger.LogWarning("Query timed out after {TimeoutSeconds} seconds", seconds);
            throw new QueryTimeoutException(TimeSpan.FromSeconds(seconds), exception);
        }
        catch (QueryTimeoutException exception)
        {
            _logger.LogWarning("Query timed out after {TimeoutSeconds} seconds", exception.Timeout.TotalSeconds);
            throw;
        }
        catch (OperationCanceledException) { _logger.LogInformation("Query cancelled"); throw; }
        catch (Exception exception)
        {
            _logger.Log(exception is DomainException or TimeoutException ? LogLevel.Warning : LogLevel.Error,
                "Query failed: {Failure}", ExceptionDiagnostics.Describe(exception));
            throw;
        }
    }

    private async Task<QueryResult> ExecuteScriptAsync(QueryExecutionContext context,
        CancellationToken historyCancellationToken, CancellationToken cancellationToken)
    {
        var session = await GetOrCreateSessionAsync(context.TabId, context.Target, cancellationToken).ConfigureAwait(false);

        var stopwatch = Stopwatch.StartNew();
        var success = false;

        try
        {
            var result = await session.EvaluateAsync(context.Script, context.Database, cancellationToken).ConfigureAwait(false);
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
            await RecordHistoryAsync(context, success, stopwatch.Elapsed, historyCancellationToken).ConfigureAwait(false);
        }
    }

    public async Task CloseTabAsync(Guid tabId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_sessions.TryRemove(tabId, out var session))
        {
            await session.DisposeAsync().ConfigureAwait(false);
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var session in _sessions.Values)
        {
            await session.DisposeAsync().ConfigureAwait(false);
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

        var session = await _sessionFactory.CreateAsync(target, cancellationToken).ConfigureAwait(false);
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
