using AwesomeAssertions;
using FakeItEasy;
using MongoTui.Application.Abstractions.Persistence;
using MongoTui.Application.Diagnostics;
using MongoTui.Application.Services;
using MongoTui.Domain.Diagnostics;
using MongoTui.Domain.Primitives;

namespace MongoTui.Application.Tests.Diagnostics;

public sealed class ProfilerPreferencesTests
{
    [Fact]
    public async Task Should_ReadPerConnectionPreferences_WhenSettingsServiceIsRecreated()
    {
        var store = A.Fake<ISettingsStore>(); var values = new Dictionary<string, string>(); var id = ConnectionId.New();
        A.CallTo(() => store.SetAsync(A<string>._, A<string>._, A<CancellationToken>._))
            .Invokes((string key, string value, CancellationToken _) => values[key] = value).Returns(Task.CompletedTask);
        A.CallTo(() => store.GetAsync(A<string>._, A<CancellationToken>._))
            .ReturnsLazily((string key, CancellationToken _) => values.GetValueOrDefault(key));
        var expected = new ProfilerPreferences { Mode = SlowQueryMode.ServerSlow, Databases = ["shop", "orders"], SlowMs = 275,
            SampleRate = .25, RestoreOnExit = false, AutoDisableMinutes = 0 };
        await new DiagnosticsSettingsService(store).SaveProfilerFormAsync(id, expected, false, CancellationToken.None);
        var service = new DiagnosticsSettingsService(store);

        var actual = await service.GetProfilerPreferencesAsync(id, CancellationToken.None);

        actual.Should().BeEquivalentTo(expected);
        service.Current.LogSlowQueries.Should().BeFalse();
        values.Keys.Should().Contain(DiagnosticsSettingsService.ProfilerLastKeyPrefix + id);
        values[DiagnosticsSettingsService.ProfilerLastKeyPrefix + id].Should().NotContain("Filter").And.NotContain("mongodb://");
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("null")]
    [InlineData("{\"Mode\":999,\"Databases\":[\"shop\"]}")]
    [InlineData("{\"Mode\":2,\"Databases\":[\"bad.name\"]}")]
    [InlineData("{\"Mode\":2,\"Databases\":[\"shop\"],\"SampleRate\":2}")]
    [InlineData("{\"Mode\":2,\"Databases\":[\"shop\"],\"AutoDisableMinutes\":-1}")]
    public async Task Should_IgnoreInvalidPreferences_WhenLoadingFormDefaults(string json)
    {
        var store = A.Fake<ISettingsStore>(); var id = ConnectionId.New();
        A.CallTo(() => store.GetAsync(DiagnosticsSettingsService.ProfilerLastKeyPrefix + id, A<CancellationToken>._)).Returns(json);

        var actual = await new DiagnosticsSettingsService(store).GetProfilerPreferencesAsync(id, CancellationToken.None);

        actual.Should().BeNull();
    }

    [Theory]
    [InlineData(ProfilingLevel.Off, SlowQueryMode.Off, false, 0)]
    [InlineData(ProfilingLevel.Slow, SlowQueryMode.ServerSlow, false, 0)]
    [InlineData(ProfilingLevel.All, SlowQueryMode.ServerAll, true, 15)]
    public void Should_UseActualServerStateAndModePolicy_WhenNoMatchingPreferencesExist(ProfilingLevel level,
        SlowQueryMode expected, bool restore, int minutes)
    {
        var state = new ProfilerState("shop", level, new() { SlowMs = 80, SampleRate = .4, FilterJson = "{ millis: { $gt: 80 } }" }, null, true, null);
        var saved = new ProfilerPreferences { Mode = SlowQueryMode.Client, SlowMs = 999 };

        var form = ProfilerFormState.From(state, new(), saved, null);

        form.Mode.Should().Be(expected); form.Options.Should().Be(state.Options);
        form.RestoreOnExit.Should().Be(restore); form.AutoDisableMinutes.Should().Be(minutes);
    }

    [Fact]
    public async Task ShouldNot_WriteSettings_WhenProfilerPreferencesAreInvalid()
    {
        var store = A.Fake<ISettingsStore>();
        var preferences = new ProfilerPreferences { Mode = SlowQueryMode.ServerSlow, Databases = ["shop"], SlowMs = -1 };

        var act = () => new DiagnosticsSettingsService(store).SaveProfilerFormAsync(ConnectionId.New(), preferences, false, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
        A.CallTo(() => store.SetAsync(A<string>._, A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
    }
}
