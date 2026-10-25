using AwesomeAssertions;
using FakeItEasy;
using Microsoft.Extensions.DependencyInjection;
using MongoTui.Application.Abstractions.Diagnostics;
using MongoTui.Application.Localization;
using MongoTui.Application.Models;
using MongoTui.Web.Components.Panels;
using MongoTui.Web.Localization;

namespace MongoTui.Web.Tests;

public sealed class LoggingSettingsPanelTests
{
    private readonly ILoggingSettings _settings = A.Fake<ILoggingSettings>();
    private readonly WebText _text = new();

    [Theory]
    [InlineData("en", false)]
    [InlineData("ru", false)]
    [InlineData("en", true)]
    [InlineData("ru", true)]
    public async Task Should_SaveSelectedLevelAndExplainOverride_When_FormSubmitted(string language, bool overridden)
    {
        _text.SetLanguage(language);
        var effective = overridden ? ApplicationLogLevel.Error : ApplicationLogLevel.Information;
        A.CallTo(() => _settings.GetAsync(A<CancellationToken>._)).Returns(new LoggingSettings(ApplicationLogLevel.Information, effective, overridden));
        A.CallTo(() => _settings.SaveAsync(ApplicationLogLevel.Verbose, A<CancellationToken>._))
            .Returns(new LoggingSettings(ApplicationLogLevel.Verbose, overridden ? effective : ApplicationLogLevel.Verbose, overridden));
        using var services = Services();
        await using var renderer = new ComponentTestRenderer(services);
        await renderer.MountAsync<LoggingSettingsPanel>(new());

        await renderer.ChangeAsync("Verbose");
        await renderer.SubmitAsync();

        A.CallTo(() => _settings.SaveAsync(ApplicationLogLevel.Verbose, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        (await renderer.TextAsync()).Should().Contain(_text[LocalizationKeys.Logging.Saved]);
        if (overridden) (await renderer.TextAsync()).Should().Contain(_text.Format(LocalizationKeys.Logging.Override, effective));
    }

    [Theory]
    [InlineData("en")]
    [InlineData("ru")]
    public async Task Should_KeepFormEditableAndReportFailure_When_PersistenceFails(string language)
    {
        _text.SetLanguage(language);
        A.CallTo(() => _settings.GetAsync(A<CancellationToken>._))
            .Returns(new LoggingSettings(ApplicationLogLevel.Information, ApplicationLogLevel.Information, false));
        A.CallTo(() => _settings.SaveAsync(A<ApplicationLogLevel>._, A<CancellationToken>._))
            .ThrowsAsync(new IOException("private storage error"));
        using var services = Services();
        await using var renderer = new ComponentTestRenderer(services);
        await renderer.MountAsync<LoggingSettingsPanel>(new());

        await renderer.SubmitAsync();

        (await renderer.TextAsync()).Should().Contain(_text[LocalizationKeys.Logging.SaveError]).And.NotContain("private storage error");
        (await renderer.AttributeAsync("#logging-level", "disabled")).Should().NotBe(true);
    }

    [Fact]
    public async Task Should_CancelSaveAndIgnoreLateResult_When_PanelClosed()
    {
        A.CallTo(() => _settings.GetAsync(A<CancellationToken>._))
            .Returns(new LoggingSettings(ApplicationLogLevel.Information, ApplicationLogLevel.Information, false));
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var token = CancellationToken.None;
        A.CallTo(() => _settings.SaveAsync(A<ApplicationLogLevel>._, A<CancellationToken>._)).ReturnsLazily(async call =>
        {
            token = call.GetArgument<CancellationToken>(1);
            started.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return new LoggingSettings(ApplicationLogLevel.Debug, ApplicationLogLevel.Debug, false);
        });
        using var services = Services();
        await using var renderer = new ComponentTestRenderer(services);
        await renderer.MountAsync<LoggingSettingsPanel>(new());
        var pending = renderer.SubmitAsync();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(3));

        await renderer.CloseAsync();
        await pending.WaitAsync(TimeSpan.FromSeconds(3));

        token.IsCancellationRequested.Should().BeTrue();
    }

    private ServiceProvider Services()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(_settings);
        services.AddSingleton(_text);
        return services.BuildServiceProvider();
    }
}
