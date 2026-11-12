using System.Collections.Concurrent;
using AwesomeAssertions;
using MongoTui.Application.Abstractions.Process;
using MongoTui.Infrastructure.Process;

namespace MongoTui.Infrastructure.Tests.Process;

public sealed class DefaultProcessRunnerTests
{
    [Fact]
    public async Task Should_CaptureOutput_When_ProcessRuns()
    {
        var runner = new DefaultProcessRunner();
        var request = new ProcessRequest("dotnet", ["--version"]);

        var result = await runner.RunAsync(request, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.StandardOutput.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Should_ReturnNonZeroExitCode_When_CommandUnknown()
    {
        var runner = new DefaultProcessRunner();
        var request = new ProcessRequest("dotnet", ["--no-such-option-xyz"]);

        var result = await runner.RunAsync(request, CancellationToken.None);

        result.Succeeded.Should().BeFalse();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Should_StreamBothPipes_WhenProcessIsRunning(bool cancel)
    {
        var runner = new DefaultProcessRunner();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var first = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var chunks = new ConcurrentQueue<ProcessOutput>();
        var request = OperatingSystem.IsWindows()
            ? new ProcessRequest("cmd.exe", ["/d", "/c", "echo first & ping -n 3 127.0.0.1 > nul & echo warning 1>&2 & echo last"])
            : new ProcessRequest("/bin/sh", ["-c", "printf 'first'; sleep 2; printf 'warning' >&2; printf 'last  \\n\\n'"]);
        request = request with
        {
            OnOutput = chunk =>
            {
                chunks.Enqueue(chunk);
                if (!chunk.IsStandardError) first.TrySetResult();
                return ValueTask.CompletedTask;
            },
        };

        var pending = runner.RunAsync(request, timeout.Token);
        await first.Task.WaitAsync(timeout.Token);

        pending.IsCompleted.Should().BeFalse();
        if (cancel)
        {
            await timeout.CancelAsync();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
            chunks.Should().Contain(chunk => chunk.Text.Contains("first"));
            return;
        }

        var result = await pending;
        result.StandardOutput.Should().Contain("first").And.Contain("last");
        result.StandardError.Should().Contain("warning");
        string.Concat(chunks.Where(chunk => !chunk.IsStandardError).Select(chunk => chunk.Text)).Should().Be(result.StandardOutput);
        string.Concat(chunks.Where(chunk => chunk.IsStandardError).Select(chunk => chunk.Text)).Should().Be(result.StandardError);
    }
}
