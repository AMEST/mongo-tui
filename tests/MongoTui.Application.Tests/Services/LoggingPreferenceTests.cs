using AwesomeAssertions;
using FakeItEasy;
using MongoTui.Application.Abstractions.Persistence;
using MongoTui.Application.Models;
using MongoTui.Application.Services;

namespace MongoTui.Application.Tests.Services;

public sealed class LoggingPreferenceTests
{
    private readonly ISettingsStore _store = A.Fake<ISettingsStore>();

    [Theory]
    [InlineData(null, ApplicationLogLevel.Information)]
    [InlineData("Debug", ApplicationLogLevel.Debug)]
    [InlineData("verbose", ApplicationLogLevel.Verbose)]
    [InlineData("Fatal", ApplicationLogLevel.Fatal)]
    [InlineData("invalid", ApplicationLogLevel.Information)]
    [InlineData("100", ApplicationLogLevel.Information)]
    public async Task Should_ReadLevelWithFallback_When_PreferenceLoaded(string? value, ApplicationLogLevel expected)
    {
        A.CallTo(() => _store.GetAsync("logging.level", A<CancellationToken>._)).Returns(value);

        var level = await new SettingsService(_store).GetLogLevelAsync(CancellationToken.None);

        level.Should().Be(expected);
    }

    [Theory]
    [InlineData(ApplicationLogLevel.Verbose)]
    [InlineData(ApplicationLogLevel.Debug)]
    [InlineData(ApplicationLogLevel.Information)]
    [InlineData(ApplicationLogLevel.Warning)]
    [InlineData(ApplicationLogLevel.Error)]
    [InlineData(ApplicationLogLevel.Fatal)]
    public async Task Should_PersistNamedLevel_When_PreferenceChanged(ApplicationLogLevel level)
    {
        await new SettingsService(_store).SetLogLevelAsync(level, CancellationToken.None);

        A.CallTo(() => _store.SetAsync("logging.level", level.ToString(), A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Should_RejectUnknownLevel_When_SavingPreference()
    {
        var action = () => new SettingsService(_store).SetLogLevelAsync((ApplicationLogLevel)42, CancellationToken.None);

        await action.Should().ThrowAsync<ArgumentOutOfRangeException>();
        A.CallTo(() => _store.SetAsync(A<string>._, A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
    }
}
