using AwesomeAssertions;
using FakeItEasy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using MongoTui.Application.Abstractions.Diagnostics;
using MongoTui.Application.Abstractions.Mongo;
using MongoTui.Application.Abstractions.Persistence;
using MongoTui.Application.Abstractions.Security;
using MongoTui.Application.Abstractions.Ssh;
using MongoTui.Application.DependencyInjection;
using MongoTui.Application.Localization;
using MongoTui.Application.Models;
using MongoTui.Web.Components.Panels;
using MongoTui.Web.Localization;

namespace MongoTui.Web.Tests;

public sealed class SettingsPanelLocalizationTests
{
    [Fact]
    public async Task Should_TranslateLoggingSection_When_LanguageChangedInSettings()
    {
        var registrations = new ServiceCollection();
        registrations.AddLogging();
        registrations.AddApplication();
        registrations.AddSingleton(A.Fake<ISettingsStore>());
        registrations.AddSingleton(A.Fake<ICryptoService>());
        registrations.AddSingleton(A.Fake<IMongoGateway>());
        registrations.AddSingleton(A.Fake<IConnectionRepository>());
        registrations.AddSingleton(A.Fake<IStorageTransaction>());
        registrations.AddSingleton(A.Fake<ISshTunnelFactory>());
        registrations.AddSingleton(A.Fake<ITwoFactorPrompt>());
        registrations.AddSingleton(A.Fake<IJSRuntime>());
        registrations.AddSingleton(A.Fake<Microsoft.AspNetCore.Components.NavigationManager>());
        var settings = A.Fake<ILoggingSettings>();
        A.CallTo(() => settings.GetAsync(A<CancellationToken>._))
            .Returns(new LoggingSettings(ApplicationLogLevel.Information, ApplicationLogLevel.Information, false));
        registrations.AddSingleton(settings);
        var text = new WebText();
        registrations.AddSingleton(text);
        await using var services = registrations.BuildServiceProvider();
        await using var renderer = new ComponentTestRenderer(services);
        await renderer.MountAsync<SettingsPanel>(new());

        await renderer.ChangeAsync("ru");

        (await renderer.TextAsync()).Should().Contain(text[LocalizationKeys.Logging.Title])
            .And.Contain(text[LocalizationKeys.Logging.Hint])
            .And.NotContain("Logging level");
    }
}
