using System.Diagnostics;
using System.Reflection;
using AwesomeAssertions;
using MongoTui.TUI.Bootstrap;

namespace MongoTui.TUI.Tests.Bootstrap;

public sealed class CommandLineTests
{
    [Fact]
    public async Task Should_PrintVersionAndExit_WhenVersionArgumentIsPassed()
    {
        var expectedVersion = typeof(ApplicationVersion).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;
        var executable = Path.Combine(AppContext.BaseDirectory,
            "mongo-tui" + (OperatingSystem.IsWindows() ? ".exe" : string.Empty));
        var info = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        info.Environment.Remove("MONGOTUI_ASKPASS_PIPE");
        info.ArgumentList.Add("--version");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        using var process = Process.Start(info)!;
        var output = await process.StandardOutput.ReadToEndAsync(timeout.Token);
        var error = await process.StandardError.ReadToEndAsync(timeout.Token);
        await process.WaitForExitAsync(timeout.Token);

        process.ExitCode.Should().Be(0);
        output.Should().Be(expectedVersion + Environment.NewLine);
        error.Should().BeEmpty();
    }
}
