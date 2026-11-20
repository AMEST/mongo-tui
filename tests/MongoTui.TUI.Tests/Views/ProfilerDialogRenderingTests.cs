using AwesomeAssertions;
using FakeItEasy;
using MongoTui.Application.Abstractions.Diagnostics;
using MongoTui.Application.Abstractions.Mongo;
using MongoTui.Application.Abstractions.Persistence;
using MongoTui.Application.Diagnostics;
using MongoTui.Application.Models;
using MongoTui.Application.Services;
using MongoTui.Domain.Diagnostics;
using MongoTui.TUI.Localization;
using MongoTui.TUI.Views;
using Terminal.Gui.App;
using Terminal.Gui.Drivers;
using Terminal.Gui.Views;

namespace MongoTui.TUI.Tests.Views;

public sealed class ProfilerDialogRenderingTests
{
    [Theory]
    [InlineData("en")]
    [InlineData("ru")]
    public void Should_PopulateNestedFormWithoutInput_WhenBackgroundStateReadCompletes(string language)
    {
        var gateway = A.Fake<IMongoDiagnosticsGateway>(); var store = A.Fake<ISettingsStore>();
        var response = new TaskCompletionSource<ProfilerState>(TaskCreationOptions.RunContinuationsAsynchronously);
        var target = new MongoConnectionTarget("mongodb://localhost");
        A.CallTo(() => gateway.GetProfilerStateAsync(target, "shop", A<CancellationToken>._)).Returns(response.Task);
        var settings = new DiagnosticsSettingsService(store);
        var services = new DiagnosticsServices(new DiagnosticsService(gateway, TimeProvider.System), new ExplainService(gateway, new SettingsService(store), settings),
            new QueryInsightsService(A.Fake<IQueryMetricsStore>(), A.Fake<IQueryMetricsRepository>(), settings), settings, new QueryMetricsQueue());
        var localizer = new ResxLocalizer(); localizer.SetLanguage(language);
        using IApplication app = Terminal.Gui.App.Application.Create(); app.Init(DriverRegistry.Names.ANSI);
        using var parent = new Dialog(); using var dialog = new ProfilerDialog(services, target, "shop", ["shop"], localizer, app);
        Task? load = null; var rendered = false;
        app.AddTimeout(TimeSpan.FromMilliseconds(20), () =>
        {
            var cells = app.Driver!.Contents!;
            var screen = string.Join('\n', Enumerable.Range(0, cells.GetLength(0)).Select(row =>
                string.Concat(Enumerable.Range(0, cells.GetLength(1)).Select(column => cells[row, column].Grapheme))));
            if (load?.IsCompleted != true || !screen.Contains("275", StringComparison.Ordinal)) return true;
            rendered = true; app.RequestStop(dialog); return false;
        });
        app.AddTimeout(TimeSpan.FromSeconds(3), () => { app.RequestStop(dialog); app.RequestStop(parent); return false; });
        app.AddTimeout(TimeSpan.Zero, () => { load = dialog.LoadAsync(); _ = CompleteAsync(response); app.Run(dialog); app.RequestStop(parent); return false; });

        app.Run(parent);

        rendered.Should().BeTrue(); dialog.Buttons.Should().OnlyContain(button => button.Enabled);
        dialog.SubViews.OfType<TextField>().Should().Contain(field => field.Text == "275");
        dialog.SubViews.OfType<TextField>().Should().Contain(field => field.Text == "0.25");
    }

    private static async Task CompleteAsync(TaskCompletionSource<ProfilerState> response)
    {
        await Task.Delay(60).ConfigureAwait(false);
        response.TrySetResult(new("shop", ProfilingLevel.Slow, new() { SlowMs = 275, SampleRate = .25 }, null, true, null));
    }
}
