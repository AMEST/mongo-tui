using AwesomeAssertions;
using FakeItEasy;
using MongoTui.Application.Abstractions.Persistence;
using MongoTui.Application.Services;

namespace MongoTui.Application.Tests.Services;

public sealed class SettingsServiceTests
{
    private readonly ISettingsStore _store = A.Fake<ISettingsStore>();
    private readonly SettingsService _sut;

    public SettingsServiceTests() => _sut = new SettingsService(_store);

    [Fact]
    public async Task Should_RoundTripLanguage()
    {
        A.CallTo(() => _store.GetAsync(A<string>._, A<CancellationToken>._)).Returns("ru");

        await _sut.SetLanguageAsync("ru", CancellationToken.None);
        var language = await _sut.GetLanguageAsync(CancellationToken.None);

        language.Should().Be("ru");
        A.CallTo(() => _store.SetAsync(A<string>._, "ru", A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Should_RoundTripDisplayMode()
    {
        A.CallTo(() => _store.GetAsync(A<string>._, A<CancellationToken>._)).Returns("Json");

        await _sut.SetDisplayModeAsync("Json", CancellationToken.None);

        (await _sut.GetDisplayModeAsync(CancellationToken.None)).Should().Be("Json");
    }

    [Fact]
    public async Task Should_UseDistinctKeys_ForLanguageAndDisplayMode()
    {
        var keys = new List<string>();
        A.CallTo(() => _store.SetAsync(A<string>._, A<string>._, A<CancellationToken>._))
            .Invokes((string key, string _, CancellationToken _) => keys.Add(key));

        await _sut.SetLanguageAsync("en", CancellationToken.None);
        await _sut.SetDisplayModeAsync("Table", CancellationToken.None);

        keys.Should().OnlyHaveUniqueItems().And.HaveCount(2);
    }

    [Theory]
    [InlineData(null, 30)]
    [InlineData("0", 0)]
    [InlineData("120", 120)]
    [InlineData("86400", 86_400)]
    [InlineData("-1", 30)]
    [InlineData("invalid", 30)]
    [InlineData("86401", 30)]
    public async Task Should_ReadQueryTimeout_WhenSettingsLoaded(string? stored, int expected)
    {
        A.CallTo(() => _store.GetAsync("query.timeoutSeconds", A<CancellationToken>._)).Returns(stored);

        var seconds = await _sut.GetQueryTimeoutSecondsAsync(CancellationToken.None);

        seconds.Should().Be(expected);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(90)]
    [InlineData(86_400)]
    public async Task Should_PersistQueryTimeout_WhenChanged(int seconds)
    {
        await _sut.SetQueryTimeoutSecondsAsync(seconds, CancellationToken.None);

        A.CallTo(() => _store.SetAsync("query.timeoutSeconds",
            seconds.ToString(System.Globalization.CultureInfo.InvariantCulture), A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(86_401)]
    public async Task ShouldThrow_ArgumentOutOfRangeException_WhenQueryTimeoutInvalid(int seconds)
    {
        var act = () => _sut.SetQueryTimeoutSecondsAsync(seconds, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
        A.CallTo(() => _store.SetAsync(A<string>._, A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
    }
}
