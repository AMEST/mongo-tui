using AwesomeAssertions;
using FakeItEasy;
using MongoTui.Application.Abstractions.Mongo;
using MongoTui.Application.Models;
using MongoTui.Application.Services;
using MongoTui.Domain.Collections;
using MongoTui.Domain.Exceptions;
using MongoTui.Domain.Primitives;
using MongoTui.Domain.Transfer;
using MongoTui.TUI.Localization;
using MongoTui.TUI.Views;
using Terminal.Gui.App;
using Terminal.Gui.Input;
using Terminal.Gui.Views;

namespace MongoTui.TUI.Tests.Views;

public sealed class TransferWizardDialogTests
{
    [Theory]
    [InlineData(false, "en", "ignore SOCKS")]
    [InlineData(false, "ru", "игнорируют SOCKS")]
    [InlineData(true, "en", "ignore SOCKS")]
    [InlineData(true, "ru", "игнорируют SOCKS")]
    public void Should_ShowLocalizedReasonAndKeepDialogOpen_WhenProxyTransferBlocked(bool import, string language, string reason)
    {
        var localizer = new ResxLocalizer();
        localizer.SetLanguage(language);
        var application = A.Fake<IApplication>();
        var transfer = A.Fake<IMongoTransferGateway>();
        A.CallTo(() => transfer.ExportAsync(A<MongoConnectionTarget>._, A<ExportRequest>._, A<CancellationToken>._))
            .Throws(new ProxyTransferNotSupportedException());
        A.CallTo(() => transfer.ImportAsync(A<MongoConnectionTarget>._, A<ImportRequest>._, A<CancellationToken>._))
            .Throws(new ProxyTransferNotSupportedException());
        var target = new MongoConnectionTarget("mongodb://node/?proxyHost=127.0.0.1&proxyPort=1080");
        var collection = new CollectionRef(DatabaseName.Create("app"), CollectionName.Create("users"));
        using ThemedDialog dialog = import
            ? new ImportWizardDialog(new ImportService(transfer), localizer, application, target, collection)
            : new ExportWizardDialog(new ExportService(transfer), localizer, application, target, collection);
        dialog.SubViews.OfType<TextField>().First().Text = "/tmp/transfer.json";

        dialog.Buttons[0].InvokeCommand(Command.Accept);

        dialog.SubViews.OfType<Label>().Should().ContainSingle(label => label.Text.Contains(reason));
        A.CallTo(() => application.RequestStop(dialog)).MustNotHaveHappened();
    }
}
