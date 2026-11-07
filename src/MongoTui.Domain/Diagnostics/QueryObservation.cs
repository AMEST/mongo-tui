using MongoTui.Domain.Primitives;

namespace MongoTui.Domain.Diagnostics;

public sealed record QueryObservation
{
    public Guid ObservationId { get; init; } = Guid.NewGuid();
    public required Guid ProcessId { get; init; }
    public required ConnectionId ConnectionId { get; init; }
    public string? Database { get; init; }
    public string? Collection { get; init; }
    public required QueryOperationKind Operation { get; init; }
    public required QuerySource Source { get; init; }
    public required DateTimeOffset StartedAt { get; init; }
    public required TimeSpan Duration { get; init; }
    public required bool Success { get; init; }
    public int? ErrorCode { get; init; }
    public long? AffectedCount { get; init; }
    public long? ReturnedCount { get; init; }
    public long? DocumentsExamined { get; init; }
    public long? KeysExamined { get; init; }
    public string? PlanSummary { get; init; }
    public bool IsSlow { get; init; }
    public bool TimedOut { get; init; }
    public bool Cancelled { get; init; }
}
