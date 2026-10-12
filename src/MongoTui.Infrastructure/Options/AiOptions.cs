namespace MongoTui.Infrastructure.Options;

/// <summary>Configuration for the OpenAI-compatible AI assistant.</summary>
public sealed class AiOptions
{
    /// <summary>Settings key under which the API key is stored.</summary>
    public const string ApiKeySettingKey = "ai.api_key";

    public string BaseUrl { get; set; } = "https://api.openai.com/v1";

    public string Model { get; set; } = "gpt-4o-mini";

    public string? ApiKey { get; set; }
}
