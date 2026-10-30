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
    [InlineData("en", "About MongoTui", "A terminal user interface for MongoDB.", "Version: ", "Co-author: ")]
    [InlineData("ru", "О MongoTui", "Терминальный интерфейс для MongoDB.", "Версия: ", "Соавтор: ")]
    public void Should_ShowLocalizedInformationAndEntryAssemblyVersion_WhenOpened(
        string language, string title, string description, string versionLabel, string coAuthorLabel)
    {
        var localizer = new ResxLocalizer();
        localizer.SetLanguage(language);
        var version = Assembly.GetEntryAssembly()!
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;

        using var dialog = new AboutDialog(localizer, A.Fake<IApplication>());

        dialog.Title.Should().Be(title);
        dialog.SubViews.OfType<Label>().Select(label => label.Text)
            .Should().Contain(["MongoTui", description, versionLabel + version,
                "MIT License", "Copyright (c) 2026 Klabukov Erik (AMEST)",
                coAuthorLabel + "Medvedev Denis (medved91)"]);
    }

    [Theory]
    [InlineData("en")]
    [InlineData("ru")]
    public void Should_KeepVersionAndLicenseVisible_WhenDialogIsLaidOut(string language)
    {
        var localizer = new ResxLocalizer();
        localizer.SetLanguage(language);
        using var dialog = new AboutDialog(localizer, A.Fake<IApplication>());
        dialog.BeginInit();
        dialog.EndInit();
        var labels = dialog.SubViews.OfType<Label>().ToArray();

        dialog.Layout(new Size(80, 24));

        foreach (var label in labels)
        {
            label.Frame.Height.Should().BeGreaterThan(0);
            label.Frame.Bottom.Should().BeLessThanOrEqualTo(dialog.Viewport.Height);
            label.Frame.Right.Should().BeLessThanOrEqualTo(dialog.Viewport.Width);
        }
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
