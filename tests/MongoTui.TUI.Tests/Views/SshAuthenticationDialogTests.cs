using AwesomeAssertions;
using FakeItEasy;
using MongoTui.TUI.Localization;
using MongoTui.TUI.Views;
using Terminal.Gui.App;
using Terminal.Gui.Input;
using Terminal.Gui.Views;

namespace MongoTui.TUI.Tests.Views;

public sealed class SshAuthenticationDialogTests
{
    [Fact]
    public void Should_ReturnCodeAndClearField_WhenSubmitted()
    {
        using var dialog = new SshAuthenticationDialog(new ResxLocalizer(), A.Fake<IApplication>(), "Code:");
        var field = dialog.SubViews.OfType<TextField>().Single();
        field.Text = "123456";

        dialog.Buttons[0].InvokeCommand(Command.Accept);

        field.Secret.Should().BeTrue();
        field.Text.Should().BeEmpty();
        dialog.Response.Should().Be("123456");
    }

    [Fact]
    public void Should_ReturnNull_WhenCancelled()
    {
        using var dialog = new SshAuthenticationDialog(new ResxLocalizer(), A.Fake<IApplication>(), "Code:");

        dialog.Buttons[1].InvokeCommand(Command.Accept);

        dialog.Response.Should().BeNull();
    }

    [Fact]
    public void Should_SubmitCode_WhenEnterPressedInResponseField()
    {
        var application = A.Fake<IApplication>();
        using var dialog = new SshAuthenticationDialog(new ResxLocalizer(), application, "Code:");
        var field = dialog.SubViews.OfType<TextField>().Single();
        field.Text = "123456";

        field.NewKeyDownEvent(Key.Enter);

        dialog.Response.Should().Be("123456");
        A.CallTo(() => application.RequestStop(dialog)).MustHaveHappenedOnceExactly();
    }
}
