using MongoTui.Domain.Primitives;

namespace MongoTui.Application.Models;

/// <summary>Everything needed to execute a query inside a tab.</summary>
public sealed record QueryExecutionContext
{
    public required Guid TabId { get; init; }

    public required ConnectionId ConnectionId { get; init; }

    public required MongoConnectionTarget Target { get; init; }

    public required string Script { get; init; }

    public string? Database { get; init; }

    public string? Collection { get; init; }
}
