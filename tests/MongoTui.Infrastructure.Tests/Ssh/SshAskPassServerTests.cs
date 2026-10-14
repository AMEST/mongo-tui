using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using FakeItEasy;
using MongoTui.Application.Abstractions.Ssh;
using MongoTui.Infrastructure.Ssh;

namespace MongoTui.Infrastructure.Tests.Ssh;

public sealed class SshAskPassServerTests
{
    [Fact]
    public async Task Should_RouteRepeatedMultilinePrompts_WhenOpenSshRequestsAuthentication()
    {
        var prompt = A.Fake<ITwoFactorPrompt>();
        A.CallTo(() => prompt.RequestAsync(A<string>._, A<CancellationToken>._)).Returns("123456");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using var server = new SshAskPassServer(prompt, timeout.Token);
        var requests = new[] { "Key passphrase:", "TPAM authentication\nVerification code:" };

        var responses = new List<string?>();
        foreach (var request in requests)
        {
            responses.Add(await ExchangeAsync(server.PipeName, request, timeout.Token));
        }

        responses.Should().Equal("123456", "123456");
        foreach (var request in requests)
        {
            A.CallTo(() => prompt.RequestAsync(request, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        }
    }

    [Fact]
    public async Task Should_CancelStartup_WhenUserCancelsPrompt()
    {
        var prompt = A.Fake<ITwoFactorPrompt>();
        A.CallTo(() => prompt.RequestAsync(A<string>._, A<CancellationToken>._)).Returns((string?)null);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using var server = new SshAskPassServer(prompt, timeout.Token);

        var response = await ExchangeAsync(server.PipeName, "Code:", timeout.Token);

        response.Should().BeNull();
        var act = async () => await server.Completion;
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    private static async Task<string?> ExchangeAsync(string pipeName, string request, CancellationToken cancellationToken)
    {
        await using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await pipe.ConnectAsync(cancellationToken);
        using var reader = new StreamReader(pipe, Encoding.UTF8, leaveOpen: true);
        await using var writer = new StreamWriter(pipe, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };
        await writer.WriteLineAsync(JsonSerializer.Serialize(request).AsMemory(), cancellationToken);
        var response = await reader.ReadLineAsync(cancellationToken);
        return response is null ? null : JsonSerializer.Deserialize<string>(response);
    }
}
