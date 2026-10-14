using System.Diagnostics;
using AwesomeAssertions;
using FakeItEasy;
using MongoTui.Application.Abstractions.Ssh;
using MongoTui.Infrastructure.Ssh;

namespace MongoTui.TUI.Tests.Services;

public sealed class SshAskPassClientTests
{
    [Theory]
    [InlineData("123456", 0, "123456")]
    [InlineData(null, 1, "")]
    public async Task Should_ReturnOnlyEphemeralResponse_WhenInvokedAsOpenSshHelper(string? response, int exitCode, string output)
    {
        var prompt = A.Fake<ITwoFactorPrompt>();
        A.CallTo(() => prompt.RequestAsync("Verification code:\nTPAM", A<CancellationToken>._)).Returns(response);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var server = new SshAskPassServer(prompt, timeout.Token);
        var executable = Path.Combine(AppContext.BaseDirectory, "mongo-tui" + (OperatingSystem.IsWindows() ? ".exe" : string.Empty));
        var info = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        info.Environment["MONGOTUI_ASKPASS_PIPE"] = server.PipeName;
        info.ArgumentList.Add("Verification code:\nTPAM");
        using var process = System.Diagnostics.Process.Start(info)!;

        var actual = await process.StandardOutput.ReadToEndAsync(timeout.Token);
        await process.WaitForExitAsync(timeout.Token);

        process.ExitCode.Should().Be(exitCode);
        actual.TrimEnd('\r', '\n').Should().Be(output);
        (await process.StandardError.ReadToEndAsync(timeout.Token)).Should().BeEmpty();
        A.CallTo(() => prompt.RequestAsync("Verification code:\nTPAM", A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }
}
