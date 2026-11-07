using System.Drawing;
using AwesomeAssertions;
using FakeItEasy;
using MongoTui.Application.Abstractions.Diagnostics;
using MongoTui.Application.Abstractions.Persistence;
using MongoTui.Application.Services;
using MongoTui.Domain.Diagnostics;
using MongoTui.Domain.Primitives;
using MongoTui.TUI.Localization;
using MongoTui.TUI.Views;
using Terminal.Gui.App;
using Terminal.Gui.Input;
using Terminal.Gui.Views;

namespace MongoTui.TUI.Tests.Views;

public sealed class QueryInsightsDialogTests
{
    [Theory]
    [InlineData("en")]
    [InlineData("ru")]
    public void Should_ShowAllObservationsInGroup_WhenGroupIsOpened(string language)
    {
        var localizer = new ResxLocalizer(); localizer.SetLanguage(language);
        var app = A.Fake<IApplication>(); A.CallTo(() => app.MainThreadId).Returns(Environment.CurrentManagedThreadId);
        var service = new QueryInsightsService(A.Fake<IQueryMetricsStore>(), A.Fake<IQueryMetricsRepository>(),
            new DiagnosticsSettingsService(A.Fake<ISettingsStore>()));
        var row = new QueryObservation { ProcessId = Guid.NewGuid(), ConnectionId = ConnectionId.New(), Database = "shop",
            Collection = "orders", Source = QuerySource.Driver, Operation = QueryOperationKind.Find,
            StartedAt = DateTimeOffset.UtcNow, Duration = TimeSpan.FromMilliseconds(200), Success = true, IsSlow = true };
        using var dialog = new QueryInsightsDialog(service, [row, row with { Duration = TimeSpan.FromMilliseconds(2), IsSlow = false }], localizer, app);
        dialog.BeginInit(); dialog.EndInit(); dialog.Layout(new Size(80, 24));
        var table = dialog.SubViews.OfType<TableView>().Single(); table.SetSelection(0, 0, false);

        table.InvokeCommand(Command.Activate);

        table.Table!.Rows.Should().Be(2);
        table.Table.ColumnNames.Should().Contain(localizer.Get("Diagnostics.DocumentsExamined"));
        (table.Table[0, 1]?.ToString()).Should().Be(200d.ToString("0.0"));
        (table.Table[1, 1]?.ToString()).Should().Be(2d.ToString("0.0"));
    }
}
