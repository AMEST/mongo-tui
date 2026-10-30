using MongoTui.Application.Localization;
using AwesomeAssertions;
using FakeItEasy;
using MongoTui.Application.Abstractions.Localization;
using MongoTui.TUI.Views;
using Terminal.Gui.App;

namespace MongoTui.TUI.Tests.Views;

public sealed class ConfirmDialogTests
{
    [Fact]
    public void Should_MakeYesButtonTheDefault_When_Constructed()
    {
        var localizer = A.Fake<ILocalizer>();
        A.CallTo(() => localizer.Get(A<string>._)).ReturnsLazily((string key) => key);
        var application = A.Fake<IApplication>();

        var dialog = new ConfirmDialog(localizer, application, "title", "message");

        dialog.Buttons.Should().HaveCount(2);
        dialog.Buttons[0].Text.Should().Be(LocalizationKeys.Common.Yes);
        dialog.DefaultAcceptView.Should().BeSameAs(dialog.Buttons[0]);
        dialog.Buttons[0].IsDefault.Should().BeTrue();
        dialog.Buttons[1].IsDefault.Should().BeFalse();
    }
}
