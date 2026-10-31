using AwesomeAssertions;
using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;
using MongoTui.Application.Abstractions.Mongo;
using MongoTui.Application.Models;
using MongoTui.Application.Services;
using MongoTui.Domain.Collections;
using MongoTui.Domain.Connections;
using MongoTui.Domain.Primitives;
using MongoTui.Domain.Transfer;
using MongoTui.TUI.Localization;
using MongoTui.TUI.Views;
using Terminal.Gui.App;
using Terminal.Gui.Drivers;
using Terminal.Gui.Input;
using Terminal.Gui.Views;

namespace MongoTui.TUI.Tests.Views;

public sealed class FileDialogPathTests
{
    [Theory]
    [InlineData("en", false)]
    [InlineData("ru", false)]
    [InlineData("en", true)]
    [InlineData("ru", true)]
    public void Should_PrefillHomePath_WhenTransferDialogOpened(string language, bool import)
    {
        using var dialog = CreateDialog(import, language);
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        var path = dialog.SubViews.OfType<TextField>().First().Text;

        if (import)
        {
            Path.TrimEndingDirectorySeparator(path).Should().Be(Path.TrimEndingDirectorySeparator(home));
            Path.EndsInDirectorySeparator(path).Should().BeTrue();
        }
        else
        {
            Path.GetDirectoryName(path).Should().Be(home);
            Path.GetFileName(path).Should().MatchRegex(@"^app\.items_\d{8}_\d{9}\.jsonl$");
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Should_UpdateOnlyGeneratedFilename_WhenExportFormatChanges(bool custom)
    {
        using var dialog = CreateDialog(import: false, "en");
        var output = dialog.SubViews.OfType<TextField>().First();
        if (custom) output.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "my-export.data");
        var previous = output.Text;
        var formats = Enum.GetNames<ExportFormat>();

        dialog.SubViews.OfType<ListView>().Single().SelectedItem = Array.IndexOf(formats, nameof(ExportFormat.Csv));

        output.Text.Should().Be(custom ? previous : Path.ChangeExtension(previous, ".csv"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Should_AppendFilenameToHomeDirectory_WhenTypingIntoOpenPath(bool ssh)
    {
        using IApplication app = Terminal.Gui.App.Application.Create();
        app.Init(DriverRegistry.Names.ANSI);
        using var dialog = ssh
            ? new SshTunnelDialog(new ResxLocalizer(), app, new SshTunnelOptions())
            : CreateDialog(import: true, "en");
        app.Begin(dialog);
        var fields = dialog.SubViews.OfType<TextField>();
        var input = ssh ? fields.Last() : fields.First();
        input.SetFocus();
        var directory = input.Text;

        app.Keyboard.RaiseKeyDownEvent(new Key('f'));

        input.Text.Should().Be(directory + "f");
    }

    private static ThemedDialog CreateDialog(bool import, string language)
    {
        var localizer = new ResxLocalizer();
        localizer.SetLanguage(language);
        var application = A.Fake<IApplication>();
        var gateway = A.Fake<IMongoTransferGateway>();
        var target = new MongoConnectionTarget("mongodb://localhost");
        var collection = new CollectionRef(DatabaseName.Create("app"), CollectionName.Create("items"));
        return import
            ? new ImportWizardDialog(new ImportService(gateway), localizer, application, target, collection)
            : new ExportWizardDialog(new ExportService(gateway, NullLogger<ExportService>.Instance), localizer, application, target, collection);
    }
}
