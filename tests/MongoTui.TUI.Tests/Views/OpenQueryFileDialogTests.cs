using System.Reflection;
using AwesomeAssertions;
using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;
using MongoTui.Application.Abstractions.Persistence;
using MongoTui.Application.Localization;
using MongoTui.Application.Services;
using MongoTui.TUI.Localization;
using MongoTui.TUI.Views;
using Terminal.Gui.App;
using Terminal.Gui.Input;
using Terminal.Gui.Views;

namespace MongoTui.TUI.Tests.Views;

public sealed class OpenQueryFileDialogTests
{
    private readonly IApplication _application = A.Fake<IApplication>();
    private readonly ISettingsStore _settings = A.Fake<ISettingsStore>();
    private readonly ResxLocalizer _localizer = new();
    private readonly QueryFileService _files;

    public OpenQueryFileDialogTests()
    {
        A.CallTo(() => _application.MainThreadId).Returns(Environment.CurrentManagedThreadId);
        A.CallTo(() => _settings.GetAsync("query.recentFiles", A<CancellationToken>._)).Returns("a.js\nb.js");
        _files = new QueryFileService(A.Fake<IQueryFileStore>(), _settings, NullLogger<QueryFileService>.Instance);
    }

    [Fact]
    public async Task Should_LoadRecentFilesAndPrefillFirst_When_Opened()
    {
        using var dialog = new OpenQueryFileDialog(_files, _localizer, _application);

        await dialog.LoadAsync();

        Field<TextField>(dialog, "_path").Text.Should().Be("a.js");
        Field<ListView>(dialog, "_recent").Source!.Count.Should().Be(2);
    }

    [Fact]
    public async Task Should_FillPath_When_RecentSelectionChanges()
    {
        using var dialog = new OpenQueryFileDialog(_files, _localizer, _application);
        await dialog.LoadAsync();

        Field<ListView>(dialog, "_recent").SelectedItem = 1;

        Field<TextField>(dialog, "_path").Text.Should().Be("b.js");
    }

    [Fact]
    public async Task Should_ReturnSelectedPath_When_Opened()
    {
        using var dialog = new OpenQueryFileDialog(_files, _localizer, _application);
        await dialog.LoadAsync();

        dialog.Buttons[1].InvokeCommand(Command.Accept);

        dialog.SelectedPath.Should().Be("a.js");
        A.CallTo(() => _application.RequestStop(dialog)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Should_ShowHintAndKeepOpen_When_PathBlank()
    {
        using var dialog = new OpenQueryFileDialog(_files, _localizer, _application);
        await dialog.LoadAsync();
        Field<TextField>(dialog, "_path").Text = "   ";

        dialog.Buttons[1].InvokeCommand(Command.Accept);

        dialog.SelectedPath.Should().BeNull();
        Field<Label>(dialog, "_status").Text.Should().Be(_localizer.Get(LocalizationKeys.QueryFiles.PathRequired));
        A.CallTo(() => _application.RequestStop(dialog)).MustNotHaveHappened();
    }

    [Fact]
    public async Task Should_ShowEmptyMessage_When_NoRecentFiles()
    {
        A.CallTo(() => _settings.GetAsync("query.recentFiles", A<CancellationToken>._)).Returns((string?)null);
        using var dialog = new OpenQueryFileDialog(_files, _localizer, _application);

        await dialog.LoadAsync();

        Field<TextField>(dialog, "_path").Text.Should().BeNullOrEmpty();
        Field<ListView>(dialog, "_recent").Source!.Count.Should().Be(1);
    }

    private static T Field<T>(object instance, string name) =>
        (T)instance.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(instance)!;
}
