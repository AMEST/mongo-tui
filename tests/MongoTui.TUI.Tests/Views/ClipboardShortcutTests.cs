using System.Reflection;
using System.Text;
using AwesomeAssertions;
using FakeItEasy;
using Microsoft.Extensions.DependencyInjection;
using MongoTui.Application.Abstractions.Ai;
using MongoTui.Application.Abstractions.Mongo;
using MongoTui.Application.Abstractions.Persistence;
using MongoTui.Application.Abstractions.Security;
using MongoTui.Application.Abstractions.Ssh;
using MongoTui.Application.DependencyInjection;
using MongoTui.Application.Models;
using MongoTui.Domain.Primitives;
using MongoTui.TUI.Bootstrap;
using MongoTui.TUI.Services;
using MongoTui.TUI.Views;
using Terminal.Gui.App;
using Terminal.Gui.Drivers;
using Terminal.Gui.Input;
using Terminal.Gui.Views;

#pragma warning disable CS0618 // TextView is superseded by the external Editor package.

namespace MongoTui.TUI.Tests.Views;

public sealed class ClipboardShortcutTests : IAsyncLifetime
{
    private static readonly MongoConnectionTarget Target = new("mongodb://localhost:27017");
    private static readonly Key Copy = new(KeyCode.C | KeyCode.CtrlMask);

    private readonly List<string> _clipboardWrites = [];
    private ServiceProvider _provider = null!;
    private IApplication _app = null!;
    private MainWindow _window = null!;
    private QueryTabsView _tabs = null!;

    public Task InitializeAsync()
    {
        var services = new ServiceCollection();
        services.AddSingleton(A.Fake<IMongoGateway>());
        services.AddSingleton(A.Fake<IMongoTransferGateway>());
        services.AddSingleton(A.Fake<IConnectionRepository>());
        services.AddSingleton(A.Fake<IKeyProvider>());
        services.AddSingleton(A.Fake<ICryptoService>());
        services.AddSingleton(A.Fake<IQueryHistoryRepository>());
        services.AddSingleton(A.Fake<IAiAssistant>());
        services.AddSingleton(A.Fake<ISettingsStore>());
        services.AddSingleton(A.Fake<IStorageTransaction>());
        services.AddSingleton(A.Fake<ISshTunnelFactory>());
        services.AddSingleton(A.Fake<ITwoFactorPrompt>());
        services.AddSingleton(A.Fake<IMongoShellSessionFactory>());
        services.AddApplication();
        services.AddTui();
        _provider = services.BuildServiceProvider();

        _window = _provider.GetRequiredService<MainWindow>();
        _app = Terminal.Gui.App.Application.Create();
        _app.Init(DriverRegistry.Names.ANSI);
        _app.Driver!.Clipboard = new TerminalClipboard(new FakeClipboard(isSupportedAlwaysFalse: true), _clipboardWrites.Add);
        _window.Application = _app;
        _window.Compose();
        _ = _app.Begin(_window);
        _app.LayoutAndDraw();

        _tabs = (QueryTabsView)_window.GetType()
            .GetField("_queryTabs", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(_window)!;

        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _app.Dispose();
        await _provider.DisposeAsync();
    }

    [Fact]
    public void Should_CopyEditorSelection_When_CtrlCPressed()
    {
        var editor = OpenEditor("hello world");
        editor.SelectAll();

        _app.Keyboard.RaiseKeyDownEvent(Copy).Should().BeTrue();

        CopiedText().Should().Be("hello world");
    }

    [Fact]
    public void Should_CopyMultilineUnicodeSelection_When_CtrlCPressed()
    {
        var editor = OpenEditor("привет\nмир");
        editor.SelectAll();

        _app.Keyboard.RaiseKeyDownEvent(Copy);

        CopiedText().Should().Be("привет\nмир");
    }

    [Fact]
    public void Should_CopyResultSelection_When_CtrlCPressed()
    {
        _ = OpenEditor("query");
        var result = _tabs.ActiveResultView!;
        result.Text = "{\"a\": 1}";
        result.SetFocus();
        result.SelectAll();

        _app.Keyboard.RaiseKeyDownEvent(Copy);

        CopiedText().Should().Be("{\"a\": 1}");
    }

    [Fact]
    public void Should_CopyTextFieldSelection_When_CtrlCPressed()
    {
        var field = new TextField { Width = 40, Text = "field value" };
        using Runnable<bool> runnable = new();
        runnable.Add(field);
        var session = _app.Begin(runnable);
        session.Should().NotBeNull();
        _app.LayoutAndDraw();
        field.SetFocus();
        field.SelectAll();

        _app.Keyboard.RaiseKeyDownEvent(Copy);
        _app.End(session!);

        CopiedText().Should().Be("field value");
    }

    private TextView OpenEditor(string text)
    {
        var model = _tabs.AddTab(ConnectionId.New(), Target, "db", "col", text);
        _app.LayoutAndDraw();
        model.Script = text;
        var editor = _tabs.ActiveEditorView!;
        editor.Text = text;
        editor.SetFocus();

        return editor;
    }

    private string CopiedText()
    {
        _clipboardWrites.Should().ContainSingle();
        var payload = _clipboardWrites[0].TrimStart('\u001b').Split(';', 3)[2].TrimEnd('\u0007');

        return Encoding.UTF8.GetString(Convert.FromBase64String(payload));
    }
}
