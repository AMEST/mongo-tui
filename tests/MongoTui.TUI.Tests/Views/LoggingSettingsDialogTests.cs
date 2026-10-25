using AwesomeAssertions;
using FakeItEasy;
using MongoTui.Application.Abstractions.Diagnostics;
using MongoTui.Application.Abstractions.Persistence;
using MongoTui.Application.Localization;
using MongoTui.Application.Models;
using MongoTui.Application.Services;
using MongoTui.Infrastructure.Diagnostics;
using MongoTui.TUI.Localization;
using MongoTui.TUI.Views;
using Terminal.Gui.App;
using Terminal.Gui.Drivers;
using Terminal.Gui.Input;
using Terminal.Gui.Views;

namespace MongoTui.TUI.Tests.Views;

public sealed class LoggingSettingsDialogTests
{
    private readonly ISettingsStore _store = A.Fake<ISettingsStore>();
    private readonly ResxLocalizer _localizer = new();
    private readonly TaskCompletionSource _save = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<string?> _load = new(TaskCreationOptions.RunContinuationsAsynchronously);

    [Theory]
    [InlineData("en", false, false)]
    [InlineData("ru", false, false)]
    [InlineData("en", false, true)]
    [InlineData("ru", false, true)]
    [InlineData("en", true, false)]
    [InlineData("ru", true, false)]
    [InlineData("en", true, true)]
    [InlineData("ru", true, true)]
    public async Task Should_UpdateNestedDialogWithoutInput_When_PreferenceOperationCompletes(string language, bool load, bool fails)
    {
        _localizer.SetLanguage(language);
        A.CallTo(() => _store.SetAsync("logging.level", "Debug", A<CancellationToken>._)).Returns(_save.Task);
        using var settings = new SerilogLoggingSettings(new SettingsService(_store), new SerilogLevelControl(null));
        using IApplication app = Terminal.Gui.App.Application.Create();
        app.Init(DriverRegistry.Names.ANSI);
        using var parent = new Dialog();
        using var dialog = new LoggingSettingsDialog(settings, _localizer, app);
        if (load) A.CallTo(() => _store.GetAsync("logging.level", A<CancellationToken>._)).Returns(_load.Task);
        else await dialog.LoadAsync(CancellationToken.None);
        var observe = Observe(app, dialog, load, fails);
        app.AddTimeout(TimeSpan.Zero, () => { app.Run(dialog); app.RequestStop(parent); return false; });

        app.Run(parent);

        var result = observe();
        result.Started.Should().BeTrue();
        result.Disabled.Should().BeTrue();
        dialog.Saved.Should().Be(!load && !fails);
        dialog.Buttons[0].Enabled.Should().Be(!load || !fails);
        if (fails) dialog.SubViews.OfType<Label>().Last().Text.Should().Be(_localizer.Get(
            load ? LocalizationKeys.Logging.LoadError : LocalizationKeys.Logging.SaveError));
        if (load && !fails) dialog.SubViews.OfType<OptionSelector>().Single().Value.Should().Be((int)ApplicationLogLevel.Debug);
    }

    private Func<(bool Started, bool Disabled)> Observe(IApplication app, LoggingSettingsDialog dialog, bool load, bool fails)
    {
        var started = false;
        var disabled = false;
        app.AddTimeout(TimeSpan.FromMilliseconds(40), () =>
        {
            if (!started)
            {
                started = true;
                if (load) _ = dialog.LoadAsync(CancellationToken.None);
                else
                {
                    dialog.SubViews.OfType<OptionSelector>().Single().Value = (int)ApplicationLogLevel.Debug;
                    dialog.Buttons[0].InvokeCommand(Command.Accept);
                    dialog.Buttons[0].InvokeCommand(Command.Accept);
                }
                disabled = !dialog.Buttons[0].Enabled;
                _ = CompleteAsync(load, fails);
                return true;
            }
            var feedback = dialog.SubViews.OfType<Label>().Last().Text;
            if (feedback == _localizer.Get(load ? LocalizationKeys.Common.Loading : LocalizationKeys.Common.Working)) return true;
            app.RequestStop(dialog);
            return false;
        });
        app.AddTimeout(TimeSpan.FromSeconds(3), () => { app.RequestStop(dialog); return false; });
        return () => (started, disabled);
    }

    private async Task CompleteAsync(bool load, bool fails)
    {
        await Task.Delay(80).ConfigureAwait(false);
        if (load)
        {
            if (fails) _load.SetException(new IOException("Private storage error"));
            else _load.SetResult("Debug");
        }
        else if (fails) _save.SetException(new IOException("Private storage error"));
        else _save.SetResult();
    }

    [Fact]
    public async Task Should_ShowEffectiveLevelAndSavedChoice_When_EnvironmentOverrideActive()
    {
        var settings = A.Fake<ILoggingSettings>();
        A.CallTo(() => settings.GetAsync(A<CancellationToken>._))
            .Returns(new LoggingSettings(ApplicationLogLevel.Debug, ApplicationLogLevel.Error, true));
        var app = A.Fake<IApplication>();
        A.CallTo(() => app.MainThreadId).Returns(Environment.CurrentManagedThreadId);
        using var dialog = new LoggingSettingsDialog(settings, _localizer, app);

        await dialog.LoadAsync(CancellationToken.None);

        dialog.SubViews.OfType<OptionSelector>().Single().Value.Should().Be((int)ApplicationLogLevel.Debug);
        dialog.SubViews.OfType<Label>().Should().Contain(label => label.Text == _localizer.Format(LocalizationKeys.Logging.Override, ApplicationLogLevel.Error));
    }
}
