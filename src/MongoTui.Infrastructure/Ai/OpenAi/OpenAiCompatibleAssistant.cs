using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MongoTui.Application.Abstractions.Ai;
using MongoTui.Application.Abstractions.Persistence;
using MongoTui.Application.QueryAssistant;
using MongoTui.Domain.Ai;
using MongoTui.Domain.Exceptions;
using MongoTui.Infrastructure.Options;

namespace MongoTui.Infrastructure.Ai.OpenAi;

/// <summary>Thin client for any OpenAI-compatible chat-completions endpoint.</summary>
public sealed class OpenAiCompatibleAssistant : IAiAssistant
{
    private readonly HttpClient _httpClient;
    private readonly AiOptions _options;
    private readonly ISettingsStore _settings;
    private readonly PromptBuilder _promptBuilder;

    public OpenAiCompatibleAssistant(
        HttpClient httpClient,
        AiOptions options,
        ISettingsStore settings,
        PromptBuilder promptBuilder)
    {
        _httpClient = httpClient;
        _options = options;
        _settings = settings;
        _promptBuilder = promptBuilder;
    }

    public Task<string> CompleteAsync(QueryAssistantRequest request, CancellationToken cancellationToken) =>
        SendAsync(_promptBuilder.Build(request), cancellationToken);

    public Task<string> CompletePromptAsync(string prompt, CancellationToken cancellationToken) =>
        SendAsync(prompt, cancellationToken);

    private async Task<string> SendAsync(string prompt, CancellationToken cancellationToken)
    {
        var apiKey = await _settings.GetAsync(AiOptions.ApiKeySettingKey, cancellationToken) ?? _options.ApiKey;

        var payload = new
        {
            model = _options.Model,
            messages = new[] { new { role = "user", content = prompt } },
        };

        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"{_options.BaseUrl.TrimEnd('/')}/chat/completions");

        if (!string.IsNullOrEmpty(apiKey))
        {
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        }

        httpRequest.Content = JsonContent.Create(payload);

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new QueryExecutionException($"AI request failed with status {(int)response.StatusCode}.");
        }

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);

        return json.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? string.Empty;
    }
}
