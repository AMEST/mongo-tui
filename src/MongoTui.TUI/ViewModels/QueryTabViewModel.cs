using MongoTui.Application.Localization;
using MongoTui.Application.Queries;
using MongoTui.Application.Abstractions.Localization;
using Serilog;
using MongoTui.Application.Models;
using MongoTui.Application.Services;
using MongoTui.Domain.Exceptions;
using MongoTui.Domain.Primitives;
using MongoTui.Domain.Collections;
using MongoTui.Domain.Queries;

namespace MongoTui.TUI.ViewModels;

/// <summary>Backs a single query tab: script, execution state and result.</summary>
public sealed class QueryTabViewModel : ObservableObject
{
    private readonly QueryService _queryService;
    private readonly ILocalizer _localizer;
    private string _title;
    private string _script;
    private bool _isExecuting;
    private QueryExecutionContext? _resultContext;

    public QueryTabViewModel(
        QueryService queryService,
        ILocalizer localizer,
        ConnectionId connectionId,
        MongoConnectionTarget target,
        string title,
        string? database = null,
        string? collection = null,
        string script = "",
        string? connectionName = null)
    {
        _queryService = queryService;
        _localizer = localizer;
        Id = Guid.NewGuid();
        ConnectionId = connectionId;
        ConnectionName = connectionName ?? connectionId.Value.ToString();
        Target = target;
        Database = database;
        Collection = collection;
        _title = title;
        _script = script;
    }

    public Guid Id { get; }

    public ConnectionId ConnectionId { get; }

    public string ConnectionName { get; }

    public MongoConnectionTarget Target { get; }

    public string? Database { get; set; }

    /// <summary>The tab database, or mongosh's URI/default database when none was selected.</summary>
    public string ContextDatabase => Database ?? ConnectionUriParser.Parse(Target.ConnectionString).Database
        ?? MongoSystemNames.DefaultDatabase;

    public string? Collection { get; set; }

    public ResultViewModel Result { get; } = new();

    /// <summary>The verified collection that produced the displayed documents, or null for ambiguous scripts.</summary>
    public CollectionRef? ResultCollection { get; private set; }

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    public string Script
    {
        get => _script;
        set => SetProperty(ref _script, value);
    }

    public bool IsExecuting
    {
        get => _isExecuting;
        private set => SetProperty(ref _isExecuting, value);
    }

    public Task ExecuteAsync(CancellationToken cancellationToken) =>
        ExecuteAsync(action => { action(); return Task.CompletedTask; }, cancellationToken);

    internal Task ExecuteAsync(Func<Action, Task> update, CancellationToken cancellationToken) =>
        ExecuteAsync(Script, update, cancellationToken);

    internal Task ExecuteAsync(string script, Func<Action, Task> update, CancellationToken cancellationToken) =>
        ExecuteAsync(new QueryExecutionContext
        {
            TabId = Id, ConnectionId = ConnectionId, Target = Target,
            Script = script, Database = Database, Collection = Collection,
        }, update, cancellationToken);

    // Only verified reads can be replayed after document mutations; the editor may contain a different script.
    internal Task RefreshAsync(Func<Action, Task> update, CancellationToken cancellationToken) =>
        _resultContext is { } context && ResultCollection is not null
            ? ExecuteAsync(context, update, cancellationToken) : Task.CompletedTask;

    private async Task ExecuteAsync(QueryExecutionContext context, Func<Action, Task> update, CancellationToken cancellationToken)
    {
        IsExecuting = true;
        ResultCollection = null;
        _resultContext = null;
        Log.Debug("Query tab {TabId}: executing ({ScriptLength} chars)", Id, context.Script.Length);

        try
        {
            QueryResult? result = null;
            Exception? failure = null;
            try
            {
                result = await _queryService.ExecuteAsync(context, cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                Log.Debug("Query tab {TabId}: execution cancelled", Id);
                return;
            }
            catch (Exception exception) { failure = exception; }
            await update(() => ApplyResult(context, result, failure)).ConfigureAwait(false);
        }
        finally
        {
            IsExecuting = false;
        }
    }

    private void ApplyResult(QueryExecutionContext context, QueryResult? result, Exception? failure)
    {
        if (failure is null)
        {
            try
            {
                Result.Load(result!);
                ResultCollection = DocumentQuerySource.Resolve(context, result!);
                _resultContext = ResultCollection is null ? null : context;
                Log.Debug("Query tab {TabId}: loaded {DocumentCount} documents in {Elapsed}", Id, result!.Documents.Count, result.Elapsed);
                return;
            }
            catch (Exception exception) { failure = exception; }
        }
        if (failure is DomainException)
        {
            Log.Warning("Query tab {TabId}: execution failed ({ExceptionType})", Id, failure.GetType().Name);
            Result.LoadError(failure is QueryTimeoutException timeout
                ? _localizer.Format(LocalizationKeys.Query.TimedOut, timeout.Timeout.TotalSeconds)
                : QueryErrorDetails.GetMessage(failure, _localizer.Get(LocalizationKeys.Query.Failed)));
        }
        else
        {
            Log.Error("Query tab {TabId}: unexpected error ({ExceptionType})", Id, failure.GetType().Name);
            Result.LoadError(QueryErrorDetails.GetMessage(failure, _localizer.Get(LocalizationKeys.Query.Failed)));
        }
    }

    public Task CloseAsync(CancellationToken cancellationToken) => _queryService.CloseTabAsync(Id, cancellationToken);
}
