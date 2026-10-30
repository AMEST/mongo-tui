using AwesomeAssertions;
using MongoTui.Web.Services;

namespace MongoTui.Web.Tests;

public sealed class WebSshPromptTests
{
    [Fact]
    public async Task Should_IsolateResponses_WhenTwoCircuitsAwaitChallenges()
    {
        var first = new WebSshPrompt();
        var second = new WebSshPrompt();
        using var cancellation = new CancellationTokenSource();
        var one = first.RequestAsync("First code", CancellationToken.None);
        var two = second.RequestAsync("Second code", cancellation.Token);

        first.Answer("123456");

        (await one).Should().Be("123456");
        two.IsCompleted.Should().BeFalse();
        first.Prompt.Should().BeNull();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => two);
        second.Prompt.Should().BeNull();
    }
}
