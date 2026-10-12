namespace MongoTui.Domain.Ai;

/// <summary>Request sent to the AI assistant.</summary>
public sealed record QueryAssistantRequest
{
    public required CollectionSchema Schema { get; init; }

    public required string Prompt { get; init; }

    public QueryAssistantMode Mode { get; init; } = QueryAssistantMode.Find;

    public bool MaskValues { get; init; } = true;

    public int SampleSize { get; init; } = 1;
}
