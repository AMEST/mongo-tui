using FakeItEasy;
using Microsoft.Extensions.DependencyInjection;
using MongoTui.Application.Abstractions.Ai;
using MongoTui.Application.Abstractions.Diagnostics;
using MongoTui.Application.Abstractions.Localization;
using MongoTui.Application.Abstractions.Mongo;
using MongoTui.Application.Abstractions.Persistence;
using MongoTui.Application.Abstractions.Security;
using MongoTui.Application.Abstractions.Ssh;
using MongoTui.Application.DependencyInjection;
using MongoTui.Application.Services;
using MongoTui.TUI.Bootstrap;
using MongoTui.TUI.Views;
using Terminal.Gui.App;
using Terminal.Gui.Drivers;

namespace MongoTui.TUI.Tests.Views;

internal sealed class TuiFeatureTestHost : IAsyncDisposable
{
    private readonly ServiceProvider _provider;
    public IApplication App { get; }
    public MainWindow Window { get; }
    public QueryTabsView Tabs { get; }
    public IMongoGateway Gateway { get; } = A.Fake<IMongoGateway>();
    public IMongoShellSession Session { get; } = A.Fake<IMongoShellSession>();
    public IConnectionRepository Connections { get; } = A.Fake<IConnectionRepository>();
    public ILocalizer Localizer { get; }

    public TuiFeatureTestHost(string language)
    {
        var sessions = A.Fake<IMongoShellSessionFactory>();
        A.CallTo(() => sessions.CreateAsync(A<MongoTui.Application.Models.MongoConnectionTarget>._, A<CancellationToken>._)).Returns(Session);
        A.CallTo(() => Session.IsRunning).Returns(true);
        var services = new ServiceCollection();
        services.AddSingleton(Gateway);
        services.AddSingleton(sessions);
        services.AddSingleton(Connections);
        services.AddSingleton(A.Fake<IMongoTransferGateway>());
        services.AddSingleton(A.Fake<IMongoUserGateway>());
        services.AddSingleton(A.Fake<ICryptoService>());
        services.AddSingleton(A.Fake<IQueryHistoryRepository>());
        services.AddSingleton(A.Fake<IAiAssistant>());
        services.AddSingleton(A.Fake<ISettingsStore>());
        services.AddSingleton(A.Fake<IStorageTransaction>());
        services.AddSingleton(A.Fake<ISshTunnelFactory>());
        services.AddSingleton(A.Fake<ITwoFactorPrompt>());
        services.AddSingleton(A.Fake<ILoggingSettings>());
        services.AddLogging();
        services.AddApplication();
        services.AddTui();
        services.AddSingleton<QueryTabsView>();
        _provider = services.BuildServiceProvider();
        _provider.GetRequiredService<SecretKeyService>().SetKek(new byte[32]);
        Localizer = _provider.GetRequiredService<ILocalizer>();
        Localizer.SetLanguage(language);
        Window = _provider.GetRequiredService<MainWindow>();
        Tabs = _provider.GetRequiredService<QueryTabsView>();
        App = Terminal.Gui.App.Application.Create();
        App.Init(DriverRegistry.Names.ANSI);
        Window.Application = App;
        Window.Compose();
    }

    public async ValueTask DisposeAsync()
    {
        App.Dispose();
        await _provider.DisposeAsync().ConfigureAwait(false);
    }
}
