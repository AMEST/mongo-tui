using AwesomeAssertions;
using FakeItEasy;
using MongoTui.Domain.Connections;
using MongoTui.TUI.Localization;
using MongoTui.TUI.Views;
using Terminal.Gui.App;
using Terminal.Gui.Input;
using Terminal.Gui.Views;

namespace MongoTui.TUI.Tests.Views;

public sealed class SshTunnelDialogTests
{
    [Fact]
    public void Should_PopulateAndSaveOptions_WhenEditingSshSettings()
    {
        var application = A.Fake<IApplication>();
        var options = new SshTunnelOptions
        {
            Enabled = true,
            Host = "tpam",
            Port = 4422,
            Username = "requester+user+system",
            LocalPort = 1080,
            PrivateKeyFile = "/keys/key"
        };
        using var dialog = new SshTunnelDialog(new ResxLocalizer(), application, options);

        dialog.Buttons[0].InvokeCommand(Command.Accept);

        dialog.Options.Should().Be(options);
        A.CallTo(() => application.RequestStop(dialog)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public void Should_KeepDialogOpen_WhenPortInvalid()
    {
        var application = A.Fake<IApplication>();
        using var dialog = new SshTunnelDialog(new ResxLocalizer(), application,
            new SshTunnelOptions { Enabled = true, Host = "jump" });
        dialog.SubViews.OfType<TextField>().ElementAt(1).Text = "bad";

        dialog.Buttons[0].InvokeCommand(Command.Accept);

        dialog.Options.Should().BeNull();
        A.CallTo(() => application.RequestStop(dialog)).MustNotHaveHappened();
    }

    [Fact]
    public void Should_KeepOptionsUnset_WhenSettingsCancelled()
    {
        var application = A.Fake<IApplication>();
        using var dialog = new SshTunnelDialog(new ResxLocalizer(), application, new SshTunnelOptions());

        dialog.Buttons[1].InvokeCommand(Command.Accept);

        dialog.Options.Should().BeNull();
        A.CallTo(() => application.RequestStop(dialog)).MustHaveHappenedOnceExactly();
    }
}
