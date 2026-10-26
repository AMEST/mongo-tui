using AwesomeAssertions;
using FakeItEasy;
using MongoTui.Domain.Collections;
using MongoTui.TUI.Localization;
using MongoTui.TUI.Views;
using Terminal.Gui.App;
using Terminal.Gui.Input;
using Terminal.Gui.Views;

namespace MongoTui.TUI.Tests.Views;

public sealed class CreateDatabaseDialogTests
{
    private readonly IApplication _application = A.Fake<IApplication>();
    private readonly ResxLocalizer _localizer = new();

    public CreateDatabaseDialogTests() =>
        A.CallTo(() => _application.MainThreadId).Returns(Environment.CurrentManagedThreadId);

    [Fact]
    public void Should_CreateDatabaseAndFirstCollection_WhenSubmitted()
    {
        CollectionRef? submitted = null;
        using var dialog = new CreateDatabaseDialog(_localizer, _application, (collection, _) =>
        {
            submitted = collection;
            return Task.CompletedTask;
        });
        Fill(dialog, " demo ", " users ");

        dialog.Buttons[0].InvokeCommand(Command.Accept);

        submitted!.Database.Value.Should().Be("demo");
        submitted.Collection.Value.Should().Be("users");
        dialog.CreatedCollection.Should().Be(submitted);
        A.CallTo(() => _application.RequestStop(dialog)).MustHaveHappenedOnceExactly();
    }

    [Theory]
    [InlineData("", "users")]
    [InlineData("bad.name", "users")]
    [InlineData("demo", "")]
    public void Should_KeepInputAndDialogOpen_WhenNameIsInvalid(string database, string collection)
    {
        var create = A.Fake<Func<CollectionRef, CancellationToken, Task>>();
        using var dialog = new CreateDatabaseDialog(_localizer, _application, create);
        Fill(dialog, database, collection);

        dialog.Buttons[0].InvokeCommand(Command.Accept);

        dialog.CreatedCollection.Should().BeNull();
        dialog.SubViews.OfType<TextField>().Select(field => field.Text).Should().Equal(database, collection);
        dialog.SubViews.OfType<Label>().Last().Text.Should().NotBeNullOrWhiteSpace();
        A.CallTo(() => create(A<CollectionRef>._, A<CancellationToken>._)).MustNotHaveHappened();
        A.CallTo(() => _application.RequestStop(dialog)).MustNotHaveHappened();
    }

    [Fact]
    public void Should_KeepServerErrorAndInput_WhenCreationFails()
    {
        using var dialog = new CreateDatabaseDialog(_localizer, _application,
            (_, _) => Task.FromException(new InvalidOperationException("Not authorized.")));
        Fill(dialog, "demo", "users");

        dialog.Buttons[0].InvokeCommand(Command.Accept);

        dialog.SubViews.OfType<Label>().Last().Text.Should().Be("Not authorized.");
        dialog.SubViews.OfType<TextField>().Select(field => field.Text).Should().Equal("demo", "users");
        A.CallTo(() => _application.RequestStop(dialog)).MustNotHaveHappened();
    }

    private static void Fill(CreateDatabaseDialog dialog, string database, string collection)
    {
        var fields = dialog.SubViews.OfType<TextField>().ToArray();
        fields[0].Text = database;
        fields[1].Text = collection;
    }
}
