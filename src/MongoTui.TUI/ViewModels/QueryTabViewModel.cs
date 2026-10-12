using Serilog;
using MongoTui.Application.Models;
using MongoTui.Application.Services;
using MongoTui.Domain.Exceptions;
using MongoTui.Domain.Primitives;

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
        Log.Debug("Query tab {TabId}: executing {Script}", Id, Script);

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
            Log.Debug("Query tab {TabId}: loaded {DocumentCount} documents in {Elapsed}", Id, result.Documents.Count, result.Elapsed);
        }
        catch (DomainException exception)
        {
            Log.Warning(exception, "Query tab {TabId}: execution failed", Id);
            Result.LoadError(exception.Message);
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Query tab {TabId}: unexpected error", Id);
            Result.LoadError(exception.Message);
        }
        finally
        {
            IsExecuting = false;
        }
    }

    public Task CloseAsync(CancellationToken cancellationToken) => _queryService.CloseTabAsync(Id, cancellationToken);
}
