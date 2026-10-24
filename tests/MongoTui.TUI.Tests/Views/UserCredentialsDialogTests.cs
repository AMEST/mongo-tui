using System.Drawing;
using AwesomeAssertions;
using FakeItEasy;
using MongoTui.TUI.Localization;
using MongoTui.TUI.Views;
using Terminal.Gui.App;
using Terminal.Gui.Input;
using Terminal.Gui.Views;

namespace MongoTui.TUI.Tests.Views;

public sealed class UserCredentialsDialogTests
{
    private readonly IApplication _application = A.Fake<IApplication>();
    private readonly ResxLocalizer _localizer = new();

    [Theory]
    [InlineData("en", true)]
    [InlineData("ru", true)]
    [InlineData("en", false)]
    [InlineData("ru", false)]
    public void Should_ShowLocalizedErrorAndKeepCredentialsOpen_WhenClipboardUnavailable(string language, bool password)
    {
        _localizer.SetLanguage(language);
        A.CallTo(() => _application.Clipboard).Returns(null);
        using var dialog = CreateDialog();

        dialog.Buttons[password ? 0 : 1].InvokeCommand(Command.Accept);

        var errorKey = password ? "Users.Credentials.PasswordCopyFailed" : "Users.Created.CopyFailed";
        dialog.SubViews.OfType<Label>().Single().Text.Should().Be(_localizer.Get(errorKey));
        A.CallTo(() => _application.RequestStop(dialog)).MustNotHaveHappened();
    }

    [Theory]
    [InlineData("en")]
    [InlineData("ru")]
    public void Should_KeepCopyButtonsVisible_WhenDialogLaidOut(string language)
    {
        _localizer.SetLanguage(language);
        using var dialog = CreateDialog();
        dialog.BeginInit();
        dialog.EndInit();

        dialog.Layout(new Size(80, 24));

        var status = dialog.SubViews.OfType<Label>().Single().FrameToScreen();
        var frames = dialog.Buttons.Select(button => button.FrameToScreen()).OrderBy(frame => frame.Left).ToList();
        frames.Should().OnlyContain(frame => dialog.FrameToScreen().Contains(frame) && frame.Top >= status.Bottom);
        frames.Zip(frames.Skip(1)).Should().OnlyContain(pair => pair.First.Right <= pair.Second.Left);
        dialog.DefaultAcceptView.Should().BeSameAs(dialog.Buttons[^1]);
    }

    private UserCredentialsDialog CreateDialog() =>
        new(_localizer, _application, "reader", "new password", "mongodb://reader:new%20password@localhost", false, true);
}
