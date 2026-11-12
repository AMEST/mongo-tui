using System.Net;
using System.Net.Sockets;
using AwesomeAssertions;

namespace MongoTui.TUI.Tests.Bootstrap;

public sealed class WebCommandLineTests
{
    [Theory]
    [InlineData("Production")]
    [InlineData("Development")]
    public async Task Should_StartWebWithoutMongoTools_WhenLaunchedFromCommandLine(string environment)
    {
        using var port = new TcpListener(IPAddress.Loopback, 0);
        port.Start();
        var number = ((IPEndPoint)port.LocalEndpoint).Port;
        port.Stop();
        await using var host = new WebCommandLineProcess(number, environment);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));

        var line = await host.Process.StandardOutput.ReadLineAsync(timeout.Token);

        line.Should().StartWith($"Sign-in link: http://127.0.0.1:{number}/#launch=");
        using var client = new HttpClient();
        using var response = await client.GetAsync($"http://127.0.0.1:{number}/", timeout.Token);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(timeout.Token)).Should().Contain("auth.js");
    }

    [Fact]
    public async Task Should_ReportFailureAndStack_WhenWebPortIsOccupied()
    {
        using var port = new TcpListener(IPAddress.Loopback, 0);
        port.Start();
        await using var host = new WebCommandLineProcess(((IPEndPoint)port.LocalEndpoint).Port, "Production");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));

        await host.Process.WaitForExitAsync(timeout.Token);

        host.Process.ExitCode.Should().Be(1);
        (await host.Process.StandardOutput.ReadToEndAsync(timeout.Token)).Should().BeEmpty();
        (await host.Process.StandardError.ReadToEndAsync(timeout.Token)).Should()
            .Contain(nameof(IOException)).And.Contain(Path.Combine(host.DataDirectory, "logs"));
        var log = await File.ReadAllTextAsync(Directory.GetFiles(Path.Combine(host.DataDirectory, "logs")).Single(), timeout.Token);
        log.Should().Contain("Application terminated unexpectedly").And.Contain("WebHost.RunAsync")
            .And.Contain(nameof(SocketException));
    }

    [Fact]
    public async Task Should_OmitConfigurationValuesFromFailureLog_WhenWebEndpointHasNoUrl()
    {
        await using var host = new WebCommandLineProcess(8765, "Production", info =>
            info.Environment["Kestrel__Endpoints__PrivateEndpoint__Protocols"] = "Http1");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));

        await host.Process.WaitForExitAsync(timeout.Token);

        host.Process.ExitCode.Should().Be(1);
        (await host.Process.StandardError.ReadToEndAsync(timeout.Token)).Should()
            .Contain(nameof(InvalidOperationException)).And.Contain(Path.Combine(host.DataDirectory, "logs"));
        var log = await File.ReadAllTextAsync(Directory.GetFiles(Path.Combine(host.DataDirectory, "logs")).Single(), timeout.Token);
        log.Should().Contain("Application terminated unexpectedly");
        var failure = log[log.LastIndexOf("Application terminated unexpectedly", StringComparison.Ordinal)..];
        failure.Should().Contain(nameof(InvalidOperationException)).And.Contain("WebHost.RunAsync")
            .And.NotContain("PrivateEndpoint");
    }
}
