using MongoTui.Domain.Primitives;

namespace MongoTui.Application.Models;

/// <summary>A single executed query recorded for the history pane.</summary>
public sealed record QueryHistoryEntry
{
    public long? Id { get; init; }

    public required ConnectionId ConnectionId { get; init; }

    public string? Database { get; init; }

    public string? Collection { get; init; }

    public required string Script { get; init; }

    public required DateTimeOffset RanAt { get; init; }

    public required bool Success { get; init; }

    public long? ElapsedMs { get; init; }
}
