using System.Collections.Concurrent;
using AwesomeAssertions;
using FakeItEasy;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MongoTui.Application.Abstractions.Persistence;
using MongoTui.Application.Models;
using MongoTui.Application.Services;
using MongoTui.Infrastructure.Diagnostics;
using MongoTui.Infrastructure.Persistence.Sqlite;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace MongoTui.Infrastructure.Tests.Diagnostics;

public sealed class SerilogLoggingSettingsTests
{
    [Fact]
    public async Task Should_ApplyLevelToExistingLoggerAndPreserveScopes_When_PreferenceSaved()
    {
        var store = A.Fake<ISettingsStore>();
        var levels = new SerilogLevelControl(null);
        using var settings = new SerilogLoggingSettings(new SettingsService(store), levels);
        var sink = new CaptureSink();
        using var serilog = Logger(levels, sink);
        using var factory = LoggerFactory.Create(builder => builder.AddSerilog(serilog, dispose: false));
        var logger = factory.CreateLogger("MongoTui.Application.QueryService");
        logger.LogDebug("Filtered before save");
        using var scope = logger.BeginScope(new Dictionary<string, object?> { ["TabId"] = "tab-one" });

        var saved = await settings.SaveAsync(ApplicationLogLevel.Verbose, CancellationToken.None);
        logger.LogTrace("Visible after save");

        saved.Should().Be(new LoggingSettings(ApplicationLogLevel.Verbose, ApplicationLogLevel.Verbose, false));
        var entry = sink.Events.Should().ContainSingle().Subject;
        entry.Level.Should().Be(LogEventLevel.Verbose);
        entry.Properties["TabId"].Should().Be(new ScalarValue("tab-one"));
        entry.Properties["SourceContext"].Should().Be(new ScalarValue("MongoTui.Application.QueryService"));
    }

    [Fact]
    public async Task Should_KeepActiveLevel_When_PersistenceFails()
    {
        var store = A.Fake<ISettingsStore>();
        A.CallTo(() => store.SetAsync(A<string>._, A<string>._, A<CancellationToken>._)).ThrowsAsync(new IOException("Storage unavailable"));
        var levels = new SerilogLevelControl(null);
        using var settings = new SerilogLoggingSettings(new SettingsService(store), levels);

        var action = () => settings.SaveAsync(ApplicationLogLevel.Fatal, CancellationToken.None);

        await action.Should().ThrowAsync<IOException>();
        levels.Application.MinimumLevel.Should().Be(LogEventLevel.Information);
        levels.Framework.MinimumLevel.Should().Be(LogEventLevel.Warning);
    }

    [Theory]
    [InlineData("Verbose", ApplicationLogLevel.Verbose)]
    [InlineData("Debug", ApplicationLogLevel.Debug)]
    [InlineData("Error", ApplicationLogLevel.Error)]
    [InlineData("Fatal", ApplicationLogLevel.Fatal)]
    public async Task Should_PreserveSavedChoiceAndEnvironmentPriority_When_SavingLevel(string value, ApplicationLogLevel effective)
    {
        var store = A.Fake<ISettingsStore>();
        var levels = new SerilogLevelControl(value);
        using var settings = new SerilogLoggingSettings(new SettingsService(store), levels);

        var result = await settings.SaveAsync(ApplicationLogLevel.Warning, CancellationToken.None);

        result.Should().Be(new LoggingSettings(ApplicationLogLevel.Warning, effective, true));
        levels.Application.MinimumLevel.Should().Be((LogEventLevel)effective);
        levels.Framework.MinimumLevel.Should().Be((LogEventLevel)Math.Max((int)effective, (int)LogEventLevel.Warning));
        A.CallTo(() => store.SetAsync("logging.level", "Warning", A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("100")]
    public void Should_UseBootstrapDefaults_When_EnvironmentLevelInvalid(string value)
    {
        var levels = new SerilogLevelControl(value);

        levels.InvalidEnvironmentOverride.Should().BeTrue();
        levels.EnvironmentOverride.Should().BeNull();
        levels.Application.MinimumLevel.Should().Be(LogEventLevel.Information);
    }

    [Fact]
    public async Task Should_LoadPersistedLevelAndObserveAnotherProcess_When_MonitorRuns()
    {
        var directory = Path.Combine(Path.GetTempPath(), "mongotui-logging-db-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var database = new SqliteDatabase(Path.Combine(directory, "settings.db"));
            database.Migrate();
            var first = new SettingsService(new SqliteSettingsStore(database));
            await first.SetLogLevelAsync(ApplicationLogLevel.Error, CancellationToken.None);
            var levels = new SerilogLevelControl(null);
            using var second = new SerilogLoggingSettings(new SettingsService(new SqliteSettingsStore(
                new SqliteDatabase(Path.Combine(directory, "settings.db")))), levels);
            using var monitor = new LoggingSettingsMonitor(second, NullLogger<LoggingSettingsMonitor>.Instance);
            await monitor.StartAsync(CancellationToken.None);
            levels.Application.MinimumLevel.Should().Be(LogEventLevel.Error);

            await first.SetLogLevelAsync(ApplicationLogLevel.Debug, CancellationToken.None);
            var deadline = DateTime.UtcNow.AddSeconds(6);
            while (levels.Application.MinimumLevel != LogEventLevel.Debug && DateTime.UtcNow < deadline) await Task.Delay(25);
            await monitor.StopAsync(CancellationToken.None);

            levels.Application.MinimumLevel.Should().Be(LogEventLevel.Debug);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static Logger Logger(SerilogLevelControl levels, ILogEventSink sink) => new LoggerConfiguration()
        .MinimumLevel.ControlledBy(levels.Application).MinimumLevel.Override("Microsoft", levels.Framework)
        .Enrich.FromLogContext().WriteTo.Sink(sink).CreateLogger();

    private sealed class CaptureSink : ILogEventSink
    {
        public ConcurrentQueue<LogEvent> Events { get; } = new();
        public void Emit(LogEvent logEvent) => Events.Enqueue(logEvent);
    }
}
