using AwesomeAssertions;
using FakeItEasy;
using MongoTui.Application.Services;
using MongoTui.Domain.Diagnostics;

namespace MongoTui.Application.Tests.Diagnostics;

public sealed class DiagnosticsSettingsServiceTests
{
    private readonly MetricsFixture _fixture = new();

    [Theory]
    [InlineData("", "", "", "")]
    [InlineData("bad", "-1", "0", "ServerAll")]
    [InlineData("bad", "600001", "1441", "4")]
    public async Task Should_FallBackToDefaults_WhenPreferencesAreInvalid(string enabled, string threshold, string retention, string mode)
    {
        Arrange(enabled, threshold, retention, mode);

        var actual = await _fixture.Settings.ReloadAsync(CancellationToken.None);

        actual.Should().Be(new ClientDiagnosticsSettings());
    }

    [Theory]
    [InlineData("0", "1")]
    [InlineData("600000", "1440")]
    public async Task Should_AcceptBounds_WhenPreferencesAreValid(string threshold, string retention)
    {
        Arrange("false", threshold, retention, "Client");

        var actual = await _fixture.Settings.ReloadAsync(CancellationToken.None);

        actual.MetricsEnabled.Should().BeFalse();
        actual.LogSlowQueries.Should().BeTrue();
        actual.SlowThresholdMs.Should().Be(int.Parse(threshold));
        actual.MemoryRetentionMinutes.Should().Be(int.Parse(retention));
    }

    [Theory]
    [InlineData(-1, 60)]
    [InlineData(600001, 60)]
    [InlineData(100, 0)]
    [InlineData(100, 1441)]
    public async Task ShouldThrow_WithoutWriting_WhenSettingsAreOutOfBounds(int threshold, int retention)
    {
        var settings = new ClientDiagnosticsSettings { SlowThresholdMs = threshold, MemoryRetentionMinutes = retention };

        var act = () => _fixture.Settings.SaveAsync(settings, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
        A.CallTo(() => _fixture.Preferences.SetAsync(A<string>._, A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task Should_RetainSnapshot_WhenReloadFails()
    {
        var expected = new ClientDiagnosticsSettings { SlowThresholdMs = 250 };
        await _fixture.Settings.SaveAsync(expected, CancellationToken.None);
        A.CallTo(() => _fixture.Preferences.GetAllAsync(A<CancellationToken>._)).ThrowsAsync(new IOException());

        var act = () => _fixture.Settings.ReloadAsync(CancellationToken.None);

        await act.Should().ThrowAsync<IOException>();
        _fixture.Settings.Current.Should().Be(expected);
    }

    private void Arrange(string enabled, string threshold, string retention, string mode) =>
        A.CallTo(() => _fixture.Preferences.GetAllAsync(A<CancellationToken>._)).Returns(new Dictionary<string, string>
        {
            [DiagnosticsSettingsService.MetricsEnabledKey] = enabled,
            [DiagnosticsSettingsService.SlowThresholdKey] = threshold,
            [DiagnosticsSettingsService.MemoryRetentionKey] = retention,
            [DiagnosticsSettingsService.SlowModeKey] = mode,
        });
}
