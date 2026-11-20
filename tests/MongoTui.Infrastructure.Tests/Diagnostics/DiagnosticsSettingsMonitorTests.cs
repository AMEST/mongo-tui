using AwesomeAssertions;
using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;
using MongoTui.Application.Abstractions.Persistence;
using MongoTui.Application.Services;
using MongoTui.Infrastructure.Diagnostics;

namespace MongoTui.Infrastructure.Tests.Diagnostics;

public sealed class DiagnosticsSettingsMonitorTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Should_AllowStartupAndShutdown_WhenPreferencesLoadOrFail(bool fail)
    {
        var store = A.Fake<ISettingsStore>();
        var settings = new DiagnosticsSettingsService(store);
        if (fail) A.CallTo(() => store.GetAllAsync(A<CancellationToken>._)).ThrowsAsync(new IOException("private"));
        else A.CallTo(() => store.GetAllAsync(A<CancellationToken>._)).Returns(new Dictionary<string, string>
            { [DiagnosticsSettingsService.SlowThresholdKey] = "250" });
        using var monitor = new DiagnosticsSettingsMonitor(settings, NullLogger<DiagnosticsSettingsMonitor>.Instance);

        await monitor.StartAsync(CancellationToken.None);

        settings.Current.SlowThresholdMs.Should().Be(fail ? 100 : 250);
        await monitor.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
    }
}
