using MongoTui.Application.Abstractions.Ai;
using MongoTui.Domain.Ai;

namespace MongoTui.Application.Services;

/// <summary>Translates a foreign query (e.g. SQL) into a MongoDB query via the AI assistant.</summary>
public sealed class TranslateQueryService
{
    private readonly IAiAssistant _assistant;

    public TranslateQueryService(IAiAssistant assistant) => _assistant = assistant;

    public Task<string> TranslateToMongoAsync(
        string source,
        QueryAssistantMode mode,
        CancellationToken cancellationToken)
    {
        var target = mode == QueryAssistantMode.Aggregate
            ? "a MongoDB aggregation pipeline as a JSON array of stages"
            : "a MongoDB find filter as a JSON object";

        var prompt =
            $"Translate the following query into {target}. " +
            $"Return only the result without explanations.\n\n{source}";

        return _assistant.CompletePromptAsync(prompt, cancellationToken);
    }
}
