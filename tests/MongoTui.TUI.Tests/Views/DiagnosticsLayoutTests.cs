using System.Drawing;
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
using Terminal.Gui.Views;

namespace MongoTui.TUI.Tests.Views;

public sealed class DiagnosticsLayoutTests
{
    [Theory]
    [InlineData("en", "overview")]
    [InlineData("ru", "overview")]
    [InlineData("en", "profiler")]
    [InlineData("ru", "profiler")]
    [InlineData("en", "settings")]
    [InlineData("ru", "settings")]
    [InlineData("en", "explain")]
    [InlineData("ru", "explain")]
    public void Should_KeepInputsAndActionsAccessible_WhenTerminalIs80By24(string language, string page)
    {
        var localizer = new ResxLocalizer(); localizer.SetLanguage(language);
        var app = A.Fake<IApplication>(); A.CallTo(() => app.MainThreadId).Returns(Environment.CurrentManagedThreadId);
        var services = Services(); var target = new MongoConnectionTarget("mongodb://localhost");
        using DiagnosticDialogBase dialog = page switch
        {
            "profiler" => new ProfilerDialog(services, target, "shop", ["shop"], localizer, app),
            "settings" => new DiagnosticsSettingsDialog(services.Settings, localizer, app),
            "explain" => new ExplainDialog(services.Explain, new QueryExecutionContext { Target = target,
                ConnectionId = MongoTui.Domain.Primitives.ConnectionId.New(), TabId = Guid.NewGuid(), Database = "shop", Script = "db.orders.find({}).toArray()" }, localizer, app),
            _ => new DiagnosticsDialog(services, target, "shop", ["shop"], localizer, app),
        };
        dialog.BeginInit(); dialog.EndInit();

        dialog.Layout(new Size(80, 24));

        foreach (var input in dialog.SubViews.OfType<TextField>())
        {
            input.Frame.Right.Should().BeLessThanOrEqualTo(dialog.Viewport.Width);
            input.Frame.Bottom.Should().BeLessThanOrEqualTo(dialog.Viewport.Height);
        }
        foreach (var button in dialog.SubViews.OfType<Button>())
        {
            button.Frame.Left.Should().BeGreaterThanOrEqualTo(0);
            button.Frame.Right.Should().BeLessThanOrEqualTo(dialog.Viewport.Width);
            button.Frame.Bottom.Should().BeLessThanOrEqualTo(dialog.Viewport.Height);
        }
#pragma warning disable CS0618
        foreach (var view in dialog.SubViews.Where(view => view.Visible && (view is TextView or TreeView<ExplainNode> or CheckBox or OptionSelector
            || view is Label && !string.IsNullOrEmpty(view.Text))))
        {
            view.Frame.Width.Should().BeGreaterThan(0, "{0} ({1}) should be visible: {2}", view.GetType().Name, view.Text, view.Frame);
            view.Frame.Height.Should().BeGreaterThan(0, "{0} ({1}) should be visible: {2}", view.GetType().Name, view.Text, view.Frame);
            view.Frame.Right.Should().BeLessThanOrEqualTo(dialog.Viewport.Width);
            view.Frame.Bottom.Should().BeLessThanOrEqualTo(dialog.Viewport.Height);
        }
#pragma warning restore CS0618
    }
    private static DiagnosticsServices Services()
    {
        var gateway = A.Fake<IMongoDiagnosticsGateway>(); var preferences = A.Fake<ISettingsStore>();
        var settings = new DiagnosticsSettingsService(preferences);
        return new(new DiagnosticsService(gateway, TimeProvider.System), new ExplainService(gateway, new SettingsService(preferences), settings),
            new QueryInsightsService(A.Fake<IQueryMetricsStore>(), A.Fake<IQueryMetricsRepository>(), settings), settings, new QueryMetricsQueue());
    }
}
