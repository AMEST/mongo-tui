using MongoTui.Application.Models;
using MongoTui.Domain.Collections;
using MongoTui.Domain.Primitives;
using MongoTui.Domain.Queries;

namespace MongoTui.Web.Services;

public sealed class WebTab(ConnectionId connectionId, string database, string? collection)
{
    public Guid Id { get; } = Guid.NewGuid();
    public ConnectionId ConnectionId { get; } = connectionId;
    public string Database { get; } = database;
    public string? Collection { get; } = collection;
    public string Script { get; set; } = string.Empty;
    public string Filter { get; set; } = "{}";
    public bool Browse { get; set; } = collection is not null;
    public ResultViewMode View { get; set; } = ResultViewMode.Table;
    public int Page { get; set; }
    public bool HasNext { get; set; }
    public QueryResult? Result { get; set; }
    public CollectionRef? EditableCollection { get; set; }
    public string? Error { get; set; }
    public bool Busy { get; internal set; }
    internal CancellationTokenSource? Cancellation { get; set; }
    internal Task? Operation { get; set; }
}
