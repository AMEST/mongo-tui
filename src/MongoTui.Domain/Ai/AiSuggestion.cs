namespace MongoTui.Domain.Ai;

/// <summary>Suggestion produced by the AI assistant.</summary>
public sealed record AiSuggestion
{
    public required string Content { get; init; }

    public string? Explanation { get; init; }
}
