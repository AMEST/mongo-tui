using MongoTui.Domain.Collections;

namespace MongoTui.Domain.Transfer;

/// <summary>Describes an export operation.</summary>
public sealed record ExportRequest
{
    public required CollectionRef Collection { get; init; }

    public required string OutputPath { get; init; }

    public required ExportFormat Format { get; init; }

    public string? FilterJson { get; init; }

    public IReadOnlyList<string>? Fields { get; init; }

    public int? Limit { get; init; }
}
