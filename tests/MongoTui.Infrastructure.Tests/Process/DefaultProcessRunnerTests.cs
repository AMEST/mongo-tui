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
}
