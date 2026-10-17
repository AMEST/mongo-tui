using Serilog;
using MongoTui.Application.Models;
using MongoTui.Application.Services;
using MongoTui.Domain.Exceptions;
using MongoTui.Domain.Primitives;
using MongoTui.Domain.Collections;
using MongoTui.TUI.Editor;

namespace MongoTui.TUI.ViewModels;

/// <summary>Backs a single query tab: script, execution state and result.</summary>
public sealed class QueryTabViewModel : ObservableObject
{
    private readonly QueryService _queryService;
    private string _title;
    private string _script;
    private bool _isExecuting;

    public QueryTabViewModel(
        QueryService queryService,
        ConnectionId connectionId,
        MongoConnectionTarget target,
        string title,
        string? database = null,
        string? collection = null,
        string script = "")
    {
        _queryService = queryService;
        Id = Guid.NewGuid();
        ConnectionId = connectionId;
        Target = target;
        Database = database;
        Collection = collection;
        _title = title;
        _script = script;
    }

    public Guid Id { get; }

    public ConnectionId ConnectionId { get; }

    public MongoConnectionTarget Target { get; }

    public string? Database { get; set; }

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

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        IsExecuting = true;
        ResultCollection = null;
        Log.Debug("Query tab {TabId}: executing ({ScriptLength} chars)", Id, Script.Length);

        try
        {
            var context = new QueryExecutionContext
            {
                TabId = Id,
                ConnectionId = ConnectionId,
                Target = Target,
                Script = Script,
                Database = Database,
                Collection = Collection,
            };

            var result = await _queryService.ExecuteAsync(context, cancellationToken);
            Result.Load(result);
            ResultCollection = result.Documents.All(document => document.Contains("_id"))
                ? DocumentQuerySource.Resolve(context.Script, context.Database) : null;
            Log.Debug("Query tab {TabId}: loaded {DocumentCount} documents in {Elapsed}", Id, result.Documents.Count, result.Elapsed);
        }
        catch (DomainException exception)
        {
            Log.Warning("Query tab {TabId}: execution failed ({ExceptionType})", Id, exception.GetType().Name);
            Result.LoadError(exception.Message);
        }
        catch (Exception exception)
        {
            Log.Error("Query tab {TabId}: unexpected error ({ExceptionType})", Id, exception.GetType().Name);
            Result.LoadError(exception.Message);
        }
        finally
        {
            IsExecuting = false;
        }
    }

    public Task CloseAsync(CancellationToken cancellationToken) => _queryService.CloseTabAsync(Id, cancellationToken);
}
