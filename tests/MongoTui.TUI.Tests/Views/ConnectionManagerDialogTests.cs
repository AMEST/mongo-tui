using System.Drawing;
using System.Text;
using AwesomeAssertions;
using FakeItEasy;
using MongoTui.Application.Abstractions.Mongo;
using MongoTui.Application.Abstractions.Persistence;
using MongoTui.Application.Abstractions.Security;
using MongoTui.Application.Abstractions.Ssh;
using MongoTui.Application.Models;
using MongoTui.Application.Services;
using MongoTui.Domain.Connections;
using MongoTui.Domain.Primitives;
using MongoTui.TUI.Localization;
using MongoTui.TUI.Views;
using Terminal.Gui.App;
using Terminal.Gui.Input;
using Terminal.Gui.Views;

namespace MongoTui.TUI.Tests.Views;

public sealed class ConnectionManagerDialogTests
{
    private readonly IConnectionRepository _repository = A.Fake<IConnectionRepository>();
    private readonly ICryptoService _crypto = A.Fake<ICryptoService>();
    private readonly IApplication _application = A.Fake<IApplication>();
    private readonly IClipboard _clipboard = A.Fake<IClipboard>();
    private readonly IMongoGateway _gateway = A.Fake<IMongoGateway>();
    private readonly ISshTunnelFactory _tunnels = A.Fake<ISshTunnelFactory>();
    private readonly ResxLocalizer _localizer = new();
    private readonly ConnectionService _connections;

    public ConnectionManagerDialogTests()
    {
        var keyProvider = A.Fake<IKeyProvider>();
        A.CallTo(() => keyProvider.UnlockWithMasterPasswordAsync("master", A<CancellationToken>._))
            .Returns(new byte[32]);
        _connections = new ConnectionService(_repository, new SecretKeyService(keyProvider, _crypto), _gateway,
            new ConnectionTunnelService(_tunnels, A.Fake<ITwoFactorPrompt>(), _gateway));
        A.CallTo(() => _application.Clipboard).Returns(_clipboard);
        A.CallTo(() => _clipboard.TrySetClipboardData(A<string>._)).Returns(true);
        _localizer.SetLanguage("en");
    }

    [Theory]
    [InlineData("mongodb://node:27017", "mongodb://bob%40example.com:p%40%3A%2F%3F%23%5B%5D%25@node:27017?authSource=admin")]
    [InlineData("mongodb://node/app?tls=true&replicaSet=rs0", "mongodb://bob%40example.com:p%40%3A%2F%3F%23%5B%5D%25@node/app?tls=true&replicaSet=rs0&authSource=admin")]
    [InlineData("mongodb+srv://cluster.example.com/?authSource=users", "mongodb+srv://bob%40example.com:p%40%3A%2F%3F%23%5B%5D%25@cluster.example.com/?authSource=users")]
    public async Task Should_CopyCompleteUri_WhenAuthenticatedProfileSelected(string uri, string expected)
    {
        await _connections.UnlockAsync("master", CancellationToken.None);
        var profile = NewProfile(uri) with
        {
            Auth = new AuthOptions { Kind = AuthKind.ScramSha256, Username = "bob@example.com", AuthDatabase = "admin" }
        };
        var secret = new EncryptedSecret(new byte[12], new byte[8]);
        A.CallTo(() => _crypto.Decrypt(A<byte[]>._, A<byte[]>._, A<byte[]>._))
            .Returns(Encoding.UTF8.GetBytes("p@:/?#[]%"));
        using var dialog = await CreateDialogAsync(new StoredConnection(NewProfile(), null), new StoredConnection(profile, secret));
        dialog.SubViews.OfType<ListView>().Single().SelectedItem = 1;

        CopyButton(dialog).InvokeCommand(Command.Accept);

        A.CallTo(() => _clipboard.TrySetClipboardData(expected)).MustHaveHappenedOnceExactly();
        Status(dialog).Should().Be("Connection URI copied to clipboard.");
        dialog.ConnectedProfile.Should().BeNull();
        dialog.ConnectedTarget.Should().BeNull();
        A.CallTo(() => _application.RequestStop(dialog)).MustNotHaveHappened();
    }

    [Theory]
    [InlineData("mongodb://node1:27017,node2:27017/app?replicaSet=rs0")]
    [InlineData("mongodb+srv://cluster.example.com/?retryWrites=true&w=majority")]
    public async Task Should_CopyOriginalUriWithoutConnecting_WhenProfileHasNoAuthentication(string uri)
    {
        var profile = NewProfile(uri) with { Ssh = new SshTunnelOptions { Enabled = true, Host = "jump" } };
        using var dialog = await CreateDialogAsync(new StoredConnection(profile, null));

        CopyButton(dialog).InvokeCommand(Command.Accept);

        A.CallTo(() => _clipboard.TrySetClipboardData(uri)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _gateway.ListDatabasesAsync(A<MongoConnectionTarget>._, A<CancellationToken>._))
            .MustNotHaveHappened();
        A.CallTo(() => _tunnels.StartAsync(A<SshTunnelOptions>._, A<ITwoFactorPrompt>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Theory]
    [InlineData("en", "Connection URI copied to clipboard.")]
    [InlineData("ru", "URI подключения скопирован в буфер обмена.")]
    public async Task Should_ShowLocalizedSuccess_WhenUriCopied(string language, string expected)
    {
        _localizer.SetLanguage(language);
        using var dialog = await CreateDialogAsync(new StoredConnection(NewProfile(), null));

        CopyButton(dialog).InvokeCommand(Command.Accept);

        Status(dialog).Should().Be(expected);
    }

    [Fact]
    public async Task Should_HideCopyUriButton_WhenNoConnectionsSaved()
    {
        using var dialog = await CreateDialogAsync();

        CopyButton(dialog).Visible.Should().BeFalse();
        CopyButton(dialog).Enabled.Should().BeFalse();
        A.CallTo(() => _clipboard.TrySetClipboardData(A<string>._)).MustNotHaveHappened();
        A.CallTo(() => _repository.GetAsync(A<ConnectionId>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task Should_ShowCopyUriButton_WhenConnectionSelected()
    {
        using var dialog = await CreateDialogAsync(new StoredConnection(NewProfile(), null));

        CopyButton(dialog).Visible.Should().BeTrue();
        CopyButton(dialog).Enabled.Should().BeTrue();
    }

    [Theory]
    [InlineData("en", "Could not copy URI to clipboard.")]
    [InlineData("ru", "Не удалось скопировать URI в буфер обмена.")]
    public async Task Should_ShowLocalizedError_WhenClipboardWriteFails(string language, string expected)
    {
        _localizer.SetLanguage(language);
        A.CallTo(() => _clipboard.TrySetClipboardData(A<string>._)).Returns(false);
        using var dialog = await CreateDialogAsync(new StoredConnection(NewProfile(), null));

        CopyButton(dialog).InvokeCommand(Command.Accept);

        Status(dialog).Should().Be(expected);
        A.CallTo(() => _application.RequestStop(dialog)).MustNotHaveHappened();
    }

    [Fact]
    public async Task Should_ShowError_WhenClipboardUnavailable()
    {
        A.CallTo(() => _application.Clipboard).Returns(null);
        using var dialog = await CreateDialogAsync(new StoredConnection(NewProfile(), null));

        CopyButton(dialog).InvokeCommand(Command.Accept);

        Status(dialog).Should().Be("Could not copy URI to clipboard.");
    }

    [Fact]
    public async Task Should_KeepClipboardUnchanged_WhenSecretsLocked()
    {
        var profile = NewProfile() with { Auth = new AuthOptions { Kind = AuthKind.ScramSha256, Username = "bob" } };
        using var dialog = await CreateDialogAsync(new StoredConnection(profile, new EncryptedSecret(new byte[12], new byte[8])));

        CopyButton(dialog).InvokeCommand(Command.Accept);

        Status(dialog).Should().Contain("locked");
        A.CallTo(() => _clipboard.TrySetClipboardData(A<string>._)).MustNotHaveHappened();
        A.CallTo(() => _application.RequestStop(dialog)).MustNotHaveHappened();
    }

    [Theory]
    [InlineData("en")]
    [InlineData("ru")]
    public async Task Should_KeepActionsAndStatusVisible_WhenDialogLaidOut(string language)
    {
        _localizer.SetLanguage(language);
        using var dialog = await CreateDialogAsync();
        dialog.BeginInit();
        dialog.EndInit();

        dialog.Layout(new Size(80, 24));

        foreach (var button in dialog.SubViews.OfType<Button>())
        {
            button.Frame.Left.Should().BeGreaterThanOrEqualTo(0);
            button.Frame.Right.Should().BeLessThanOrEqualTo(dialog.Viewport.Width);
        }
        var list = dialog.SubViews.OfType<ListView>().Single();
        var status = dialog.SubViews.OfType<Label>().Single();
        list.Frame.Bottom.Should().BeLessThanOrEqualTo(CopyButton(dialog).Frame.Top);
        CopyButton(dialog).Frame.Bottom.Should().BeLessThanOrEqualTo(status.Frame.Top);
        status.Frame.Right.Should().BeLessThanOrEqualTo(dialog.Viewport.Width);
        dialog.DefaultAcceptView.Should().BeSameAs(dialog.Buttons[0]);
    }

    private async Task<ConnectionManagerDialog> CreateDialogAsync(params StoredConnection[] stored)
    {
        A.CallTo(() => _repository.GetAllAsync(A<CancellationToken>._)).Returns(stored);
        foreach (var connection in stored)
        {
            A.CallTo(() => _repository.GetAsync(connection.Profile.Id, A<CancellationToken>._)).Returns(connection);
        }
        var dialog = new ConnectionManagerDialog(_connections, _localizer, _application);
        await dialog.LoadAsync();
        dialog.SubViews.OfType<ListView>().Single().SelectedItem = stored.Length == 0 ? null : 0;
        return dialog;
    }

    private Button CopyButton(ConnectionManagerDialog dialog) =>
        dialog.SubViews.OfType<Button>().Single(button => button.Text == _localizer.Get("Connections.CopyUri"));

    private static string Status(ConnectionManagerDialog dialog) => dialog.SubViews.OfType<Label>().Single().Text;

    private static ConnectionProfile NewProfile(string uri = "mongodb://localhost:27017") =>
        ConnectionProfile.Create(ConnectionId.New(), "test", uri);
}
