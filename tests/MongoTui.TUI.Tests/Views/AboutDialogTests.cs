using System.Drawing;
using System.Reflection;
using AwesomeAssertions;
using FakeItEasy;
using MongoTui.TUI.Localization;
using MongoTui.TUI.Views;
using Terminal.Gui.App;
using Terminal.Gui.Input;
using Terminal.Gui.Views;

namespace MongoTui.TUI.Tests.Views;

public sealed class AboutDialogTests
{
    [Theory]
    [InlineData("en", "About MongoTui", "A terminal user interface for MongoDB.", "Version: ")]
    [InlineData("ru", "О MongoTui", "Терминальный интерфейс для MongoDB.", "Версия: ")]
    public void Should_ShowLocalizedInformationAndEntryAssemblyVersion_WhenOpened(
        string language, string title, string description, string versionLabel)
    {
        var localizer = new ResxLocalizer();
        localizer.SetLanguage(language);
        var version = Assembly.GetEntryAssembly()!
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;

        using var dialog = new AboutDialog(localizer, A.Fake<IApplication>());

        dialog.Title.Should().Be(title);
        dialog.SubViews.OfType<Label>().Select(label => label.Text)
            .Should().Contain(["MongoTui", description, versionLabel + version]);
    }

    [Theory]
    [InlineData("en")]
    [InlineData("ru")]
    public void Should_KeepVersionVisible_WhenDialogIsLaidOut(string language)
    {
        var localizer = new ResxLocalizer();
        localizer.SetLanguage(language);
        using var dialog = new AboutDialog(localizer, A.Fake<IApplication>());
        dialog.BeginInit();
        dialog.EndInit();
        var version = dialog.SubViews.OfType<Label>()
            .Single(label => label.Text == localizer.Format("About.Version",
                Assembly.GetEntryAssembly()!
                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion));

        dialog.Layout(new Size(80, 24));

        version.Frame.Height.Should().BeGreaterThan(0);
        version.Frame.Bottom.Should().BeLessThanOrEqualTo(dialog.Viewport.Height);
        version.Frame.Right.Should().BeLessThanOrEqualTo(dialog.Viewport.Width);
    }

    [Fact]
    public void Should_CloseDialog_WhenOkIsAccepted()
    {
        var application = A.Fake<IApplication>();
        using var dialog = new AboutDialog(new ResxLocalizer(), application);

        dialog.Buttons[0].InvokeCommand(Command.Accept);

        A.CallTo(() => application.RequestStop(dialog)).MustHaveHappenedOnceExactly();
    }
}
