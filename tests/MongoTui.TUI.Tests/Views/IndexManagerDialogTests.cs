using System.Reflection;
using AwesomeAssertions;
using FakeItEasy;
using MongoTui.Application.Abstractions.Mongo;
using MongoTui.Application.Models;
using MongoTui.Application.Services;
using MongoTui.Domain.Collections;
using MongoTui.Domain.Primitives;
using MongoTui.TUI.Localization;
using MongoTui.TUI.Views;
using Terminal.Gui.App;
using Terminal.Gui.Input;
using Terminal.Gui.Views;

namespace MongoTui.TUI.Tests.Views;

public sealed class IndexManagerDialogTests
{
    private static readonly MongoConnectionTarget Target = new("mongodb://localhost:27017");
    private static readonly CollectionRef Collection =
        new(DatabaseName.Create("app"), CollectionName.Create("users"));

    private readonly IMongoGateway _gateway = A.Fake<IMongoGateway>();
    private readonly ResxLocalizer _localizer = new();
    private readonly IApplication _application = A.Fake<IApplication>();
    private readonly IndexManagerDialog _dialog;

    public IndexManagerDialogTests()
    {
        A.CallTo(() => _application.MainThreadId).Returns(Environment.CurrentManagedThreadId);
        A.CallTo(() => _gateway.ListIndexesAsync(Target, Collection, A<CancellationToken>._))
            .Returns(Task.FromResult<IReadOnlyList<IndexInfo>>([]));
        A.CallTo(() => _gateway.GetCollectionStatsAsync(Target, Collection, A<CancellationToken>._))
            .Returns(Task.FromResult(new CollectionStats { Name = "users" }));

        _dialog = new IndexManagerDialog(new IndexService(_gateway), _localizer, _application, Target, Collection);
    }

    [Fact]
    public void Should_ForwardCheckboxState_When_CreatingIndex()
    {
        Field<TextField>("_keysField").Text = "{ \"email\": 1 }";
        Field<TextField>("_nameField").Text = "email_1";
        Field<CheckBox>("_uniqueCheck").Value = CheckState.Checked;
        Field<CheckBox>("_backgroundCheck").Value = CheckState.Checked;

        _dialog.Buttons[1].InvokeCommand(Command.Accept);

        A.CallTo(() => _gateway.CreateIndexAsync(
                Target, Collection, "{ \"email\": 1 }", "email_1", true, true, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public void Should_DefaultCheckboxesToUnchecked()
    {
        Field<CheckBox>("_uniqueCheck").Value.Should().Be(CheckState.UnChecked);
        Field<CheckBox>("_backgroundCheck").Value.Should().Be(CheckState.UnChecked);
    }

    [Fact]
    public async Task Should_ShowFieldsAndUniqueFlag_WhenIndexesAreLoaded()
    {
        A.CallTo(() => _gateway.ListIndexesAsync(Target, Collection, A<CancellationToken>._))
            .Returns(new[] { new IndexInfo { Name = "email_date", Unique = true,
                Fields = [new IndexField("email", IndexDirection.Ascending), new IndexField("date", IndexDirection.Descending)] } });

        await _dialog.LoadAsync();

        Field<Label>("_details").Text.Should().Contain("email: 1").And.Contain("date: -1")
            .And.Contain(_localizer.Get(MongoTui.Application.Localization.LocalizationKeys.Index.UniqueBadge));
        _dialog.Buttons[2].Enabled.Should().BeTrue();
    }

    [Fact]
    public async Task ShouldNot_AllowDroppingIdIndex_WhenSelected()
    {
        A.CallTo(() => _gateway.ListIndexesAsync(Target, Collection, A<CancellationToken>._))
            .Returns(new[] { new IndexInfo { Name = "_id_", Fields = [new IndexField("_id", IndexDirection.Ascending)] } });
        await _dialog.LoadAsync();

        _dialog.Buttons[2].InvokeCommand(Command.Accept);

        _dialog.Buttons[2].Enabled.Should().BeFalse();
        A.CallTo(() => _gateway.DropIndexAsync(Target, Collection, A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    private T Field<T>(string name) =>
        (T)_dialog.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(_dialog)!;
}
