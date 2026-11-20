using System.Reflection;
using AwesomeAssertions;
using FakeItEasy;
using MongoTui.Application.Abstractions.Diagnostics;
using MongoTui.Application.Abstractions.Mongo;
using MongoTui.Application.Abstractions.Persistence;
using MongoTui.Application.Diagnostics;
using MongoTui.Application.Models;
using MongoTui.Application.Services;
using MongoTui.Domain.Diagnostics;
using MongoTui.Domain.Primitives;
using MongoTui.TUI.Localization;
using MongoTui.TUI.Views;
using Terminal.Gui.App;
using Terminal.Gui.Input;
using Terminal.Gui.Views;

namespace MongoTui.TUI.Tests.Views;

public sealed class ProfilerDialogTests
{
    private readonly IMongoDiagnosticsGateway _gateway = A.Fake<IMongoDiagnosticsGateway>();
    private readonly ISettingsStore _store = A.Fake<ISettingsStore>();
    private readonly MongoConnectionTarget _target = new("mongodb://localhost") { ConnectionId = ConnectionId.New() };
    private readonly ResxLocalizer _localizer = new();
    private ProfilerState _actual = new("shop", ProfilingLevel.Off, new(), null, true, null);

    public ProfilerDialogTests()
    {
        var values = new Dictionary<string, string>();
        A.CallTo(() => _store.SetAsync(A<string>._, A<string>._, A<CancellationToken>._))
            .Invokes((string key, string value, CancellationToken _) => values[key] = value).Returns(Task.CompletedTask);
        A.CallTo(() => _store.GetAsync(A<string>._, A<CancellationToken>._))
            .ReturnsLazily((string key, CancellationToken _) => values.GetValueOrDefault(key));
        A.CallTo(() => _gateway.GetProfilerStateAsync(_target, "shop", A<CancellationToken>._)).ReturnsLazily(() => _actual);
        A.CallTo(() => _gateway.SetProfilerLevelAsync(_target, "shop", A<ProfilingLevel>._, A<ProfilerOptions>._,
            A<ProfilerFilterAction>._, A<CancellationToken>._)).ReturnsLazily((MongoConnectionTarget _, string _, ProfilingLevel level,
                ProfilerOptions options, ProfilerFilterAction _, CancellationToken _) => _actual = _actual with { Level = level, Options = options });
    }

    [Theory]
    [InlineData("en")]
    [InlineData("ru")]
    public async Task Should_RestoreSavedFields_WhenServerSlowDialogIsReopened(string language)
    {
        _localizer.SetLanguage(language); var app = FakeApp(); var firstServices = Services();
        using (var first = new ProfilerDialog(firstServices, _target, "shop", ["shop"], _localizer, app))
        {
            await first.LoadAsync();
            Field<OptionSelector>(first, "_mode").Value = (int)SlowQueryMode.ServerSlow;
            Field<TextField>(first, "_threshold").Text = "275"; Field<TextField>(first, "_sample").Text = "0.25";
            first.Buttons.Single(button => button.Text == _localizer.Get("Diagnostics.Apply")).InvokeCommand(Command.Accept);
            firstServices.Server.Leases.Single().ExpiresAt.Should().BeNull();
        }
        using var reopened = new ProfilerDialog(Services(), _target, "shop", ["shop"], _localizer, app);

        await reopened.LoadAsync();

        Field<OptionSelector>(reopened, "_mode").Value.Should().Be((int)SlowQueryMode.ServerSlow);
        Field<TextField>(reopened, "_threshold").Text.Should().Be("275"); Field<TextField>(reopened, "_sample").Text.Should().Be("0.25");
        Field<CheckBox>(reopened, "_restore").Value.Should().Be(CheckState.UnChecked);
        Field<TextField>(reopened, "_minutes").Text.Should().Be("0");
        A.CallTo(() => _gateway.SetProfilerLevelAsync(_target, "shop", ProfilingLevel.Slow, A<ProfilerOptions>._,
            A<ProfilerFilterAction>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Theory]
    [InlineData("en")]
    [InlineData("ru")]
    public async Task Should_ShowServerOff_WhenSavedPreferencesRequestedSlowMode(string language)
    {
        _localizer.SetLanguage(language); var services = Services();
        await services.Settings.SaveProfilerPreferencesAsync(_target.ConnectionId, new() { Mode = SlowQueryMode.ServerSlow,
            Databases = ["shop"], SlowMs = 275, SampleRate = .25 }, CancellationToken.None);
        _actual = _actual with { Options = new() { SlowMs = 80, SampleRate = .5, FilterJson = "{ millis: { $gt: 80 } }" } };
        using var dialog = new ProfilerDialog(services, _target, "shop", ["shop"], _localizer, FakeApp());

        await dialog.LoadAsync();

        Field<OptionSelector>(dialog, "_mode").Value.Should().Be((int)SlowQueryMode.Off);
        Field<TextField>(dialog, "_threshold").Text.Should().Be("80"); Field<TextField>(dialog, "_sample").Text.Should().Be("0.5");
        Field<TextField>(dialog, "_filter").Text.Should().Be(_actual.Options.FilterJson);
        A.CallTo(() => _gateway.SetProfilerLevelAsync(_target, "shop", A<ProfilingLevel>._, A<ProfilerOptions>._,
            A<ProfilerFilterAction>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public void Should_DefaultFullProfilerToTimedRestoration_WhenModeIsChanged()
    {
        using var dialog = new ProfilerDialog(Services(), _target, "shop", ["shop"], _localizer, FakeApp());

        Field<OptionSelector>(dialog, "_mode").Value = (int)SlowQueryMode.ServerAll;

        Field<CheckBox>(dialog, "_restore").Value.Should().Be(CheckState.Checked);
        Field<TextField>(dialog, "_minutes").Text.Should().Be("15");
    }

    private DiagnosticsServices Services()
    {
        var settings = new DiagnosticsSettingsService(_store);
        return new(new DiagnosticsService(_gateway, TimeProvider.System), new ExplainService(_gateway, new SettingsService(_store), settings),
            new QueryInsightsService(A.Fake<IQueryMetricsStore>(), A.Fake<IQueryMetricsRepository>(), settings), settings, new QueryMetricsQueue());
    }
    private static IApplication FakeApp()
    {
        var app = A.Fake<IApplication>(); A.CallTo(() => app.MainThreadId).Returns(Environment.CurrentManagedThreadId); return app;
    }
    private static T Field<T>(object value, string name) => (T)value.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(value)!;
}
