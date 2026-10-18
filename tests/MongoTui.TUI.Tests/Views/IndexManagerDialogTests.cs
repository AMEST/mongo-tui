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

    private T Field<T>(string name) =>
        (T)_dialog.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(_dialog)!;
}
