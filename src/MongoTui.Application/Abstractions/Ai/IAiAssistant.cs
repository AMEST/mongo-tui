using MongoTui.Domain.Ai;

namespace MongoTui.Application.Abstractions.Ai;

/// <summary>Sends query-assistant requests to an AI model.</summary>
public interface IAiAssistant
{
    Task<string> CompleteAsync(QueryAssistantRequest request, CancellationToken cancellationToken);

    /// <summary>Sends a raw prompt and returns the model's text response.</summary>
    Task<string> CompletePromptAsync(string prompt, CancellationToken cancellationToken);
}
