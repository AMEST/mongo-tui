using AwesomeAssertions;
using FakeItEasy;
using Microsoft.Extensions.DependencyInjection;
using MongoTui.Application.Abstractions.Diagnostics;
using MongoTui.Application.Abstractions.Mongo;
using MongoTui.Application.Abstractions.Persistence;
using MongoTui.Application.Diagnostics;
using MongoTui.Application.Models;
using MongoTui.Application.Services;
using MongoTui.Domain.Diagnostics;
using MongoTui.Domain.Primitives;
using MongoTui.Web.Components.Panels;
using MongoTui.Web.Localization;

namespace MongoTui.Web.Tests;

public sealed class ProfilerPanelTests
{
    [Theory]
    [InlineData("en")]
    [InlineData("ru")]
    public async Task Should_RequireConfirmationAndDisplayActualState_WhenFullProfilerIsRequested(string language)
    {
        var gateway = A.Fake<IMongoDiagnosticsGateway>(); var target = new MongoConnectionTarget("mongodb://localhost");
        var original = new ProfilerState("shop", ProfilingLevel.Off, new(), null, true, null);
        A.CallTo(() => gateway.GetProfilerStateAsync(target, "shop", A<CancellationToken>._)).Returns(original);
        A.CallTo(() => gateway.SetProfilerLevelAsync(target, "shop", ProfilingLevel.All, A<ProfilerOptions>._, A<ProfilerFilterAction>._,
            A<CancellationToken>._)).Returns(original with { Level = ProfilingLevel.All });
        using var services = Services(gateway, language);
        await using var renderer = new ComponentTestRenderer(services);
        await renderer.MountAsync<ProfilerPanel>(new() { [nameof(ProfilerPanel.Target)] = target, [nameof(ProfilerPanel.Database)] = "shop" });
        await renderer.ChangeAsync("#profiler-mode", "ServerAll");
        ((await renderer.AttributeAsync("#profiler-minutes", "value"))?.ToString()).Should().Be("15");
        (await renderer.AttributeAsync("#profiler-restore", "checked")).Should().Be(true);
        await renderer.ClickAsync("#profiler-apply");
        A.CallTo(() => gateway.SetProfilerLevelAsync(target, "shop", A<ProfilingLevel>._, A<ProfilerOptions>._, A<ProfilerFilterAction>._,
            A<CancellationToken>._)).MustNotHaveHappened();

        await renderer.ClickAsync("#profiler-confirm");

        A.CallTo(() => gateway.SetProfilerLevelAsync(target, "shop", ProfilingLevel.All, A<ProfilerOptions>._, A<ProfilerFilterAction>._,
            A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        (await renderer.TextAsync()).Should().Contain("shop: All → All");
    }
    [Theory]
    [InlineData("en")]
    [InlineData("ru")]
    public async Task Should_LoadActualOptionsAndSavedPolicy_WhenServerSlowPanelIsOpened(string language)
    {
        var gateway = A.Fake<IMongoDiagnosticsGateway>(); var id = ConnectionId.New();
        var target = new MongoConnectionTarget("mongodb://localhost") { ConnectionId = id };
        var actual = new ProfilerState("shop", ProfilingLevel.Slow, new() { SlowMs = 275, SampleRate = .25, FilterJson = "{ millis: { $gt: 80 } }" }, null, true, null);
        A.CallTo(() => gateway.GetProfilerStateAsync(target, "shop", A<CancellationToken>._)).Returns(actual);
        var store = A.Fake<ISettingsStore>();
        var saved = new ProfilerPreferences { Mode = SlowQueryMode.ServerSlow, Databases = ["shop"], SlowMs = 999, SampleRate = .7,
            RestoreOnExit = true, AutoDisableMinutes = 30 };
        A.CallTo(() => store.GetAsync(DiagnosticsSettingsService.ProfilerLastKeyPrefix + id, A<CancellationToken>._))
            .Returns(System.Text.Json.JsonSerializer.Serialize(saved));
        using var services = Services(gateway, language, store); await using var renderer = new ComponentTestRenderer(services);

        await renderer.MountAsync<ProfilerPanel>(new() { [nameof(ProfilerPanel.Target)] = target, [nameof(ProfilerPanel.Database)] = "shop" });

        ((await renderer.AttributeAsync("#profiler-mode", "value"))?.ToString()).Should().Be("ServerSlow");
        ((await renderer.AttributeAsync("#profiler-threshold", "value"))?.ToString()).Should().Be("275");
        ((await renderer.AttributeAsync("#profiler-sample", "value"))?.ToString()).Should().Be("0.25");
        ((await renderer.AttributeAsync("#profiler-filter", "value"))?.ToString()).Should().Be(actual.Options.FilterJson);
        ((await renderer.AttributeAsync("#profiler-minutes", "value"))?.ToString()).Should().Be("30");
        (await renderer.AttributeAsync("#profiler-restore", "checked")).Should().Be(true);
        A.CallTo(() => gateway.SetProfilerLevelAsync(target, "shop", A<ProfilingLevel>._, A<ProfilerOptions>._, A<ProfilerFilterAction>._,
            A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Theory]
    [InlineData("en")]
    [InlineData("ru")]
    public async Task Should_RestoreFormWithoutReapplyingServerMode_WhenPanelIsReopened(string language)
    {
        var gateway = A.Fake<IMongoDiagnosticsGateway>();
        var target = new MongoConnectionTarget("mongodb://localhost") { ConnectionId = ConnectionId.New() };
        var actual = new ProfilerState("shop", ProfilingLevel.Off, new(), null, true, null);
        A.CallTo(() => gateway.GetProfilerStateAsync(target, "shop", A<CancellationToken>._)).ReturnsLazily(() => actual);
        A.CallTo(() => gateway.SetProfilerLevelAsync(target, "shop", A<ProfilingLevel>._, A<ProfilerOptions>._,
            A<ProfilerFilterAction>._, A<CancellationToken>._)).ReturnsLazily((MongoConnectionTarget _, string _, ProfilingLevel level,
                ProfilerOptions options, ProfilerFilterAction _, CancellationToken _) => actual = actual with { Level = level, Options = options });
        var store = MemoryStore();
        using (var first = Services(gateway, language, store))
        {
            await using var editor = new ComponentTestRenderer(first);
            await editor.MountAsync<ProfilerPanel>(new() { [nameof(ProfilerPanel.Target)] = target, [nameof(ProfilerPanel.Database)] = "shop" });
            await editor.ChangeAsync("#profiler-mode", "ServerSlow"); await editor.ChangeAsync("#profiler-threshold", "475");
            await editor.ChangeAsync("#profiler-sample", "0.3"); await editor.ClickAsync("#profiler-apply");
            first.GetRequiredService<DiagnosticsServices>().Server.Leases.Single().ExpiresAt.Should().BeNull();
        }
        using var reopened = Services(gateway, language, store); await using var renderer = new ComponentTestRenderer(reopened);

        await renderer.MountAsync<ProfilerPanel>(new() { [nameof(ProfilerPanel.Target)] = target, [nameof(ProfilerPanel.Database)] = "shop" });

        ((await renderer.AttributeAsync("#profiler-mode", "value"))?.ToString()).Should().Be("ServerSlow");
        ((await renderer.AttributeAsync("#profiler-threshold", "value"))?.ToString()).Should().Be("475");
        ((await renderer.AttributeAsync("#profiler-sample", "value"))?.ToString()).Should().Be("0.3");
        ((await renderer.AttributeAsync("#profiler-minutes", "value"))?.ToString()).Should().Be("0");
        (await renderer.AttributeAsync("#profiler-restore", "checked")).Should().NotBe(true);
        A.CallTo(() => gateway.SetProfilerLevelAsync(target, "shop", ProfilingLevel.Slow, A<ProfilerOptions>._,
            A<ProfilerFilterAction>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    private static ISettingsStore MemoryStore()
    {
        var store = A.Fake<ISettingsStore>(); var values = new Dictionary<string, string>();
        A.CallTo(() => store.SetAsync(A<string>._, A<string>._, A<CancellationToken>._))
            .Invokes((string key, string value, CancellationToken _) => values[key] = value).Returns(Task.CompletedTask);
        A.CallTo(() => store.GetAsync(A<string>._, A<CancellationToken>._))
            .ReturnsLazily((string key, CancellationToken _) => values.GetValueOrDefault(key));
        return store;
    }

    [Fact]
    public async Task Should_ShowAppliedStateWithSaveWarning_WhenFormPreferencesCannotBeWritten()
    {
        var gateway = A.Fake<IMongoDiagnosticsGateway>(); var target = new MongoConnectionTarget("mongodb://localhost") { ConnectionId = ConnectionId.New() };
        var before = new ProfilerState("shop", ProfilingLevel.Off, new(), null, true, null);
        A.CallTo(() => gateway.GetProfilerStateAsync(target, "shop", A<CancellationToken>._)).Returns(before);
        A.CallTo(() => gateway.SetProfilerLevelAsync(target, "shop", ProfilingLevel.Slow, A<ProfilerOptions>._,
            A<ProfilerFilterAction>._, A<CancellationToken>._)).Returns(before with { Level = ProfilingLevel.Slow });
        var store = A.Fake<ISettingsStore>();
        A.CallTo(() => store.SetAsync(A<string>._, A<string>._, A<CancellationToken>._)).ThrowsAsync(new IOException());
        using var services = Services(gateway, "en", store); await using var renderer = new ComponentTestRenderer(services);
        await renderer.MountAsync<ProfilerPanel>(new() { [nameof(ProfilerPanel.Target)] = target, [nameof(ProfilerPanel.Database)] = "shop" });
        await renderer.ChangeAsync("#profiler-mode", "ServerSlow");

        await renderer.ClickAsync("#profiler-apply");

        var text = await renderer.TextAsync();
        text.Should().Contain("shop: Slow → Slow").And.Contain(services.GetRequiredService<WebText>()["Diagnostics.ProfilerPreferencesSaveFailed"]);
        ((await renderer.AttributeAsync("#profiler-mode", "value"))?.ToString()).Should().Be("ServerSlow");
    }

    [Fact]
    public async Task Should_ShowActualServerState_WhenSavedPreferencesCannotBeRead()
    {
        var gateway = A.Fake<IMongoDiagnosticsGateway>(); var target = new MongoConnectionTarget("mongodb://localhost") { ConnectionId = ConnectionId.New() };
        A.CallTo(() => gateway.GetProfilerStateAsync(target, "shop", A<CancellationToken>._))
            .Returns(new ProfilerState("shop", ProfilingLevel.Slow, new() { SlowMs = 275, SampleRate = .25 }, null, true, null));
        var store = A.Fake<ISettingsStore>();
        A.CallTo(() => store.GetAsync(A<string>._, A<CancellationToken>._)).ThrowsAsync(new IOException());
        using var services = Services(gateway, "en", store); await using var renderer = new ComponentTestRenderer(services);

        await renderer.MountAsync<ProfilerPanel>(new() { [nameof(ProfilerPanel.Target)] = target, [nameof(ProfilerPanel.Database)] = "shop" });

        ((await renderer.AttributeAsync("#profiler-mode", "value"))?.ToString()).Should().Be("ServerSlow");
        ((await renderer.AttributeAsync("#profiler-threshold", "value"))?.ToString()).Should().Be("275");
        (await renderer.TextAsync()).Should().Contain(services.GetRequiredService<WebText>()["Diagnostics.ProfilerPreferencesLoadFailed"]);
    }

    private static ServiceProvider Services(IMongoDiagnosticsGateway gateway, string language, ISettingsStore? preferences = null)
    {
        var registrations = new ServiceCollection(); registrations.AddLogging();
        var text = new WebText(); text.SetLanguage(language); registrations.AddSingleton(text);
        preferences ??= A.Fake<ISettingsStore>(); var settings = new DiagnosticsSettingsService(preferences);
        registrations.AddSingleton(new DiagnosticsServices(new DiagnosticsService(gateway, TimeProvider.System),
            new ExplainService(gateway, new SettingsService(preferences), settings),
            new QueryInsightsService(A.Fake<IQueryMetricsStore>(), A.Fake<IQueryMetricsRepository>(), settings), settings, new QueryMetricsQueue()));
        return registrations.BuildServiceProvider();
    }
}
