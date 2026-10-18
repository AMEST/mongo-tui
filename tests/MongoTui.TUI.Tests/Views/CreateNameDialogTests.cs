using System.Drawing;
using AwesomeAssertions;
using FakeItEasy;
using MongoTui.Domain.Primitives;
using MongoTui.TUI.Localization;
using MongoTui.TUI.Views;
using Terminal.Gui.App;
using Terminal.Gui.Input;
using Terminal.Gui.Views;

namespace MongoTui.TUI.Tests.Views;

public sealed class CreateNameDialogTests
{
    private readonly IApplication _application = A.Fake<IApplication>();
    private readonly ResxLocalizer _localizer = new();

    [Theory]
    [InlineData("en", "Create.DatabaseTitle", "Create database")]
    [InlineData("ru", "Create.CollectionTitle", "Создание коллекции")]
    public void Should_SubmitTrimmedNameAndClose_WhenCreationSucceeds(string language, string key, string title)
    {
        _localizer.SetLanguage(language);
        string? submitted = null;
        using var dialog = new CreateNameDialog(_localizer, _application, _localizer.Get(key), name =>
        {
            submitted = name;
            return Task.CompletedTask;
        });
        dialog.SubViews.OfType<TextField>().Single().Text = " users ";

        dialog.Buttons[0].InvokeCommand(Command.Accept);

        dialog.Title.Should().Be(title);
        submitted.Should().Be("users");
        dialog.CreatedName.Should().Be("users");
        A.CallTo(() => _application.RequestStop(dialog)).MustHaveHappenedOnceExactly();
    }

    [Theory]
    [InlineData("")]
    [InlineData("bad.name")]
    public void Should_KeepDialogOpen_WhenDatabaseNameInvalid(string name)
    {
        using var dialog = new CreateNameDialog(_localizer, _application, "title", value =>
        {
            DatabaseName.Create(value);
            return Task.CompletedTask;
        });
        dialog.SubViews.OfType<TextField>().Single().Text = name;

        dialog.Buttons[0].InvokeCommand(Command.Accept);

        dialog.CreatedName.Should().BeNull();
        dialog.SubViews.OfType<Label>().Last().Text.Should().NotBeNullOrWhiteSpace();
        A.CallTo(() => _application.RequestStop(dialog)).MustNotHaveHappened();
    }

    [Fact]
    public void Should_ShowServerError_WhenCreationFails()
    {
        using var dialog = new CreateNameDialog(_localizer, _application, "title",
            _ => Task.FromException(new InvalidOperationException("Permission denied.")));

        dialog.Buttons[0].InvokeCommand(Command.Accept);

        dialog.CreatedName.Should().BeNull();
        dialog.SubViews.OfType<Label>().Last().Text.Should().Be("Permission denied.");
        A.CallTo(() => _application.RequestStop(dialog)).MustNotHaveHappened();
    }

    [Fact]
    public void Should_NotCreate_WhenCancelled()
    {
        var create = A.Fake<Func<string, Task>>();
        using var dialog = new CreateNameDialog(_localizer, _application, "title", create);

        dialog.Buttons[1].InvokeCommand(Command.Accept);

        dialog.CreatedName.Should().BeNull();
        A.CallTo(() => create(A<string>._)).MustNotHaveHappened();
        A.CallTo(() => _application.RequestStop(dialog)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public void Should_KeepNameAndErrorVisible_WhenLaidOut()
    {
        using var dialog = new CreateNameDialog(_localizer, _application, "title", _ => Task.CompletedTask);
        dialog.BeginInit();
        dialog.EndInit();

        dialog.Layout(new Size(80, 24));

        dialog.SubViews.OfType<TextField>().Single().Frame.Width.Should().BeGreaterThan(0);
        dialog.SubViews.OfType<Label>().Last().FrameToScreen().Bottom
            .Should().BeLessThanOrEqualTo(dialog.Buttons[0].FrameToScreen().Top);
    }
}
