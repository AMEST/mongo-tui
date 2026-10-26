using MongoTui.Application.Diagnostics;
using MongoTui.Application.Localization;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using MongoTui.Application.Models;
using MongoTui.Application.Queries;
using MongoTui.Application.Services;
using MongoTui.Domain.Collections;
using MongoTui.Domain.Exceptions;
using MongoTui.Domain.Primitives;
using MongoTui.Domain.Queries;
using MongoTui.Web.Localization;

namespace MongoTui.Web.Services;

/// <summary>Owns results, cancellation and query tabs for one browser circuit.</summary>
public sealed class QueryWorkspace(
    QueryService queries, ConnectionCatalog connections,
    WebQueryLimiter limiter, WebText text, ILogger<QueryWorkspace> logger) : IAsyncDisposable
{
    public const int PageSize = 50;
    private const int MaximumTabs = 12;
    private const int MaximumScriptCharacters = 256 * 1024;
    private readonly List<WebTab> _tabs = [];
    private readonly CancellationTokenSource _lifetime = new();
    public IReadOnlyList<WebTab> Tabs => _tabs;
    public WebTab? Selected { get; private set; }
    public event Action? Changed;

    public WebTab Open(ConnectionId connectionId, string database, string? collection, string? script = null)
    {
        if (_tabs.Count >= MaximumTabs) throw new DomainException(text[LocalizationKeys.Error.TabLimit]);
        var tab = new WebTab(connectionId, database, collection)
        {
            Script = script ?? (collection is null ? "db.getCollectionNames()"
                : $"db.getCollection({JsonSerializer.Serialize(collection)}).find({{}}).limit({PageSize}).toArray()"),
            Browse = collection is not null && script is null,
        };
        _tabs.Add(tab);
        Select(tab);
        return tab;
    }

    public void Select(WebTab tab) { Selected = tab; Changed?.Invoke(); }

    public async Task ExecuteAsync(WebTab tab)
    {
        if (tab.Busy || !_tabs.Contains(tab)) return;
        var request = new QueryRequest(tab.Script, tab.Browse, tab.Filter, tab.Page);
        tab.Cancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        tab.Busy = true;
        tab.Error = null;
        Changed?.Invoke();
        tab.Operation = ExecuteCoreAsync(tab, request, tab.Cancellation.Token);
        try { await tab.Operation; }
        finally
        {
            tab.Busy = false;
            tab.Cancellation.Dispose();
            tab.Cancellation = null;
            tab.Operation = null;
            Changed?.Invoke();
        }
    }

    public void Cancel(WebTab tab) => tab.Cancellation?.Cancel();

    public async Task CloseAsync(WebTab tab)
    {
        Cancel(tab);
        if (tab.Operation is { } operation) await operation;
        await queries.CloseTabAsync(tab.Id, CancellationToken.None);
        _tabs.Remove(tab);
        tab.Result = null;
        if (Selected == tab) Selected = _tabs.LastOrDefault();
        Changed?.Invoke();
    }

    public async Task CloseConnectionAsync(ConnectionId id)
    {
        foreach (var tab in _tabs.Where(item => item.ConnectionId == id).ToArray()) await CloseAsync(tab);
        await connections.DisconnectAsync(id);
    }

    public async Task PageAsync(WebTab tab, int direction)
    {
        if (tab.Busy || tab.Page + direction < 0 || direction > 0 && !tab.HasNext) return;
        tab.Page += direction;
        if (tab.Browse) await ExecuteAsync(tab);
        else
        {
            tab.HasNext = (tab.Page + 1) * PageSize < tab.Result?.Documents.Count;
            Changed?.Invoke();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _lifetime.CancelAsync();
        foreach (var tab in _tabs.ToArray()) await CloseAsync(tab);
        _lifetime.Dispose();
    }

    private async Task ExecuteCoreAsync(WebTab tab, QueryRequest request, CancellationToken cancellationToken)
    {
        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            [LogPropertyNames.ConnectionId] = tab.ConnectionId.Value, [LogPropertyNames.TabId] = tab.Id,
        });
        try
        {
            await limiter.EnterAsync(cancellationToken);
            try { await LoadAsync(tab, request, cancellationToken); }
            finally { limiter.Exit(); }
        }
        catch (QueryTimeoutException exception) { tab.Error = text.Format(LocalizationKeys.Query.TimedOut, exception.Timeout.TotalSeconds); }
        catch (OperationCanceledException) { tab.Error = text[LocalizationKeys.Query.Cancelled]; }
        catch (Exception exception)
        {
            tab.Error = exception is DomainException ? SafeError.Message(exception) : text[LocalizationKeys.Error.Query];
        }
    }

    private async Task LoadAsync(WebTab tab, QueryRequest request, CancellationToken cancellationToken)
    {
        var connection = connections.Get(tab.ConnectionId);
        if (request.Browse && tab.Collection is not null)
        {
            var collection = new CollectionRef(DatabaseName.Create(tab.Database), CollectionName.Create(tab.Collection));
            var result = await queries.FindAsync(connection.Target, collection, request.Filter,
                new QueryExecutionOptions { Skip = request.Page * PageSize, Limit = PageSize + 1 }, cancellationToken);
            tab.HasNext = result.Documents.Count > PageSize;
            tab.Result = result with { Documents = result.Documents.Take(PageSize).ToArray() };
            tab.EditableCollection = collection;
            return;
        }
        if (request.Script.Length > MaximumScriptCharacters) throw new DomainException(text[LocalizationKeys.Error.ScriptSize]);
        var scriptResult = await queries.ExecuteAsync(new QueryExecutionContext
        {
            TabId = tab.Id, ConnectionId = tab.ConnectionId, Target = connection.Target,
            Database = tab.Database, Collection = tab.Collection, Script = request.Script,
        }, cancellationToken);
        var source = scriptResult.Documents.All(document => document.Contains(MongoSystemNames.IdField))
            ? DocumentQuerySource.Resolve(request.Script, tab.Database) : null;
        tab.Result = scriptResult;
        tab.Page = 0;
        tab.HasNext = tab.Result.Documents.Count > PageSize;
        tab.EditableCollection = source;
    }

    private sealed record QueryRequest(string Script, bool Browse, string Filter, int Page);
}
