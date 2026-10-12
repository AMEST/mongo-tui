using AwesomeAssertions;
using FakeItEasy;
using MongoTui.Application.Abstractions.Ai;
using MongoTui.Application.Services;
using MongoTui.Domain.Ai;

namespace MongoTui.Application.Tests.Services;

public sealed class TranslateQueryServiceTests
{
    private readonly IAiAssistant _assistant = A.Fake<IAiAssistant>();
    private readonly TranslateQueryService _sut;

    public TranslateQueryServiceTests() => _sut = new TranslateQueryService(_assistant);

    [Fact]
    public async Task Should_ReturnAssistantOutput_When_Translated()
    {
        A.CallTo(() => _assistant.CompletePromptAsync(A<string>._, A<CancellationToken>._))
            .Returns("db.users.find({})");

        var result = await _sut.TranslateToMongoAsync("SELECT * FROM users", QueryAssistantMode.Find, CancellationToken.None);

        result.Should().Be("db.users.find({})");
    }

    [Fact]
    public async Task Should_IncludeSourceAndTarget_When_PromptSent()
    {
        string? captured = null;
        A.CallTo(() => _assistant.CompletePromptAsync(A<string>._, A<CancellationToken>._))
            .Invokes((string prompt, CancellationToken _) => captured = prompt)
            .Returns("x");

        await _sut.TranslateToMongoAsync("SELECT 1", QueryAssistantMode.Aggregate, CancellationToken.None);

        captured.Should().Contain("SELECT 1");
        captured.Should().Contain("aggregation");
    }
}
