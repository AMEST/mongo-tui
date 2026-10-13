namespace MongoTui.Application.Models;

/// <summary>Resolved AI assistant configuration.</summary>
public sealed record AiConfiguration(string BaseUrl, string Model, string? ApiKey)
{
    /// <summary>Whether the assistant has enough configuration to be used.</summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(BaseUrl)
        && !string.IsNullOrWhiteSpace(Model)
        && !string.IsNullOrWhiteSpace(ApiKey);
}
