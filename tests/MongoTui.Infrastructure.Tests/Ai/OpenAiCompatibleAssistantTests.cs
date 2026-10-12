using System.Net;
using System.Text;
using AwesomeAssertions;
using MongoTui.Application.QueryAssistant;
using MongoTui.Domain.Ai;
using MongoTui.Domain.Collections;
using MongoTui.Domain.Exceptions;
using MongoTui.Domain.Primitives;
using MongoTui.Infrastructure.Ai.OpenAi;
using MongoTui.Infrastructure.Options;
using MongoTui.Infrastructure.Tests.Fakes;

namespace MongoTui.Infrastructure.Tests.Ai;

public sealed class OpenAiCompatibleAssistantTests
{
    [Fact]
    public async Task Should_ReturnContent_When_ResponseOk()
    {
        var handler = new StubHandler(HttpStatusCode.OK, "{\"choices\":[{\"message\":{\"content\":\"db.users.find({})\"}}]}");
        var sut = CreateAssistant(handler);

        var content = await sut.CompleteAsync(CreateRequest(), CancellationToken.None);

        content.Should().Be("db.users.find({})");
    }

    [Fact]
    public async Task Should_SendBearerToken_When_ApiKeyConfigured()
    {
        var handler = new StubHandler(HttpStatusCode.OK, "{\"choices\":[{\"message\":{\"content\":\"x\"}}]}");
        var sut = CreateAssistant(handler, apiKey: "secret");

        await sut.CompleteAsync(CreateRequest(), CancellationToken.None);

        handler.LastRequest!.Headers.Authorization!.Scheme.Should().Be("Bearer");
        handler.LastRequest.Headers.Authorization.Parameter.Should().Be("secret");
    }

    [Fact]
    public async Task ShouldThrow_QueryExecutionException_When_ResponseFails()
    {
        var handler = new StubHandler(HttpStatusCode.InternalServerError, "{}");
        var sut = CreateAssistant(handler);

        var act = async () => await sut.CompleteAsync(CreateRequest(), CancellationToken.None);

        await act.Should().ThrowAsync<QueryExecutionException>();
    }

    private static OpenAiCompatibleAssistant CreateAssistant(StubHandler handler, string? apiKey = null)
    {
        var httpClient = new HttpClient(handler);
        var options = new AiOptions { BaseUrl = "https://api.test/v1", Model = "gpt-4o-mini", ApiKey = apiKey };

        return new OpenAiCompatibleAssistant(httpClient, options, new InMemorySettingsStore(), new PromptBuilder());
    }

    private static QueryAssistantRequest CreateRequest() =>
        new()
        {
            Schema = new CollectionSchema
            {
                Collection = new CollectionRef(DatabaseName.Create("app"), CollectionName.Create("users")),
                Fields = [new SchemaField("name", "string")],
            },
            Prompt = "find all",
        };

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _response;

        public StubHandler(HttpStatusCode status, string response)
        {
            _status = status;
            _response = response;
        }

        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;

            return Task.FromResult(new HttpResponseMessage(_status)
            {
                Content = new StringContent(_response, Encoding.UTF8, "application/json"),
            });
        }
    }
}
