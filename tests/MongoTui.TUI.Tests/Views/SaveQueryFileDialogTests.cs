using AwesomeAssertions;
using FakeItEasy;
using MongoTui.Application.Localization;
using MongoTui.Domain.Exceptions;
using MongoTui.TUI.Localization;
using MongoTui.TUI.Views;
using Terminal.Gui.App;
using Terminal.Gui.Input;
using Terminal.Gui.Views;

namespace MongoTui.TUI.Tests.Views;

public sealed class SaveQueryFileDialogTests
{
    private readonly IApplication _application = A.Fake<IApplication>();
    private readonly ResxLocalizer _localizer = new();

    [Theory]
    [InlineData("en")]
    [InlineData("ru")]
    public void Should_SubmitTrimmedPathAndClose_WhenSavingSucceeds(string language)
    {
        _localizer.SetLanguage(language);
        string? submitted = null;
        using var dialog = new SaveQueryFileDialog(_localizer, _application, "/tmp/query.js", path =>
        {
            submitted = path;
            return Task.CompletedTask;
        });
        dialog.SubViews.OfType<TextField>().Single().Text = "  /tmp/active users.js  ";

        dialog.Buttons[1].InvokeCommand(Command.Accept);

        submitted.Should().Be("/tmp/active users.js");
        dialog.Path.Should().Be("/tmp/active users.js");
        dialog.Saved.Should().BeTrue();
        A.CallTo(() => _application.RequestStop(dialog)).MustHaveHappenedOnceExactly();
    }

    [Theory]
    [InlineData("en")]
    [InlineData("ru")]
    public void Should_KeepDialogOpenAndShowHint_WhenPathBlank(string language)
    {
        _localizer.SetLanguage(language);
        var save = A.Fake<Func<string, Task>>();
        using var dialog = new SaveQueryFileDialog(_localizer, _application, string.Empty, save);
        dialog.SubViews.OfType<TextField>().Single().Text = "   ";

        dialog.Buttons[1].InvokeCommand(Command.Accept);

        dialog.Saved.Should().BeFalse();
        dialog.SubViews.OfType<Label>().Last().Text.Should().Be(_localizer.Get(LocalizationKeys.QueryFiles.PathRequired));
        A.CallTo(() => save(A<string>._)).MustNotHaveHappened();
        A.CallTo(() => _application.RequestStop(dialog)).MustNotHaveHappened();
    }

    [Fact]
    public void Should_ShowServerError_WhenSavingFails()
    {
        using var dialog = new SaveQueryFileDialog(_localizer, _application, "/tmp/query.js",
            _ => Task.FromException(new DomainException("Access to the path is denied.")));

        dialog.Buttons[1].InvokeCommand(Command.Accept);

        dialog.Saved.Should().BeFalse();
        dialog.SubViews.OfType<Label>().Last().Text.Should().Contain("denied");
        A.CallTo(() => _application.RequestStop(dialog)).MustNotHaveHappened();
    }

    [Fact]
    public void Should_NotSave_WhenCancelled()
    {
        var save = A.Fake<Func<string, Task>>();
        using var dialog = new SaveQueryFileDialog(_localizer, _application, "/tmp/query.js", save);

        dialog.Buttons[2].InvokeCommand(Command.Accept);

        dialog.Saved.Should().BeFalse();
        A.CallTo(() => save(A<string>._)).MustNotHaveHappened();
        A.CallTo(() => _application.RequestStop(dialog)).MustHaveHappenedOnceExactly();
    }
}
