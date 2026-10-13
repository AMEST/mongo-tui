using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MongoTui.Application.Abstractions.Ai;
using MongoTui.Application.QueryAssistant;
using MongoTui.Application.Services;
using MongoTui.Domain.Ai;
using MongoTui.Domain.Exceptions;

namespace MongoTui.Infrastructure.Ai.OpenAi;

/// <summary>Thin client for any OpenAI-compatible chat-completions endpoint.</summary>
public sealed class OpenAiCompatibleAssistant : IAiAssistant
{
    private readonly HttpClient _httpClient;
    private readonly AiSettingsService _settings;
    private readonly PromptBuilder _promptBuilder;

    public OpenAiCompatibleAssistant(
        HttpClient httpClient,
        AiSettingsService settings,
        PromptBuilder promptBuilder)
    {
        _httpClient = httpClient;
        _settings = settings;
        _promptBuilder = promptBuilder;
    }

    public Task<string> CompleteAsync(QueryAssistantRequest request, CancellationToken cancellationToken) =>
        SendAsync(_promptBuilder.Build(request), cancellationToken);

    public Task<string> CompletePromptAsync(string prompt, CancellationToken cancellationToken) =>
        SendAsync(prompt, cancellationToken);

    private async Task<string> SendAsync(string prompt, CancellationToken cancellationToken)
    {
        var configuration = await _settings.GetAsync(cancellationToken);

        if (!configuration.IsConfigured)
        {
            throw new DomainException("The AI assistant is not configured. Set the URL, model and API key in Settings.");
        }

        var payload = new
        {
            model = configuration.Model,
            messages = new[] { new { role = "user", content = prompt } },
        };

        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"{configuration.BaseUrl.TrimEnd('/')}/chat/completions");

        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", configuration.ApiKey);
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
