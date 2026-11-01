using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using FakeItEasy;
using MongoTui.Application.Abstractions.Mongo;
using MongoTui.Application.Abstractions.Persistence;
using MongoTui.Application.Abstractions.Security;
using MongoTui.Application.Abstractions.Ssh;
using MongoTui.Application.Models;
using MongoTui.Application.Services;
using MongoTui.Domain.Connections;
using MongoTui.Domain.Primitives;
using MongoTui.Web.Localization;
using MongoTui.Web.Services;

namespace MongoTui.Web.Tests;

internal sealed class WorkspaceFixture : IAsyncDisposable
{
    public IMongoGateway Gateway { get; } = A.Fake<IMongoGateway>();
    public IConnectionRepository Repository { get; } = A.Fake<IConnectionRepository>();
    public ICryptoService Crypto { get; } = A.Fake<ICryptoService>();
    public IMongoShellSession Shell { get; } = A.Fake<IMongoShellSession>();
    public ISettingsStore Settings { get; } = A.Fake<ISettingsStore>();
    public IQueryHistoryRepository History { get; } = A.Fake<IQueryHistoryRepository>();
    public WebText Text { get; } = new();
    public ConnectionProfile Profile { get; }
    public ConnectionService Connections { get; }
    public QueryWorkspace Workspace { get; }
    public ConnectionCatalog Catalog { get; }
    public WebQueryLimiter Limiter { get; } = new();
    private readonly QueryService _queries;
    private readonly ConnectionTunnelService _tunnels;

    public ILoggerFactory Loggers { get; }

    public WorkspaceFixture(ILoggerFactory? loggerFactory = null, string? connectionString = null)
    {
        Profile = ConnectionProfile.Create(ConnectionId.New(), "Test", connectionString ?? "mongodb://localhost:27017");
        Loggers = loggerFactory ?? NullLoggerFactory.Instance;
        var secrets = new SecretKeyService(Crypto);
        secrets.SetKek(new byte[32]);
        _tunnels = new(A.Fake<ISshTunnelFactory>(), A.Fake<ITwoFactorPrompt>(), Gateway);
        Connections = new(Repository, secrets, Gateway, _tunnels, Loggers.CreateLogger<ConnectionService>());
        Catalog = new(Connections, _tunnels, Gateway, Loggers.CreateLogger<ConnectionCatalog>());
        A.CallTo(() => Repository.GetAsync(Profile.Id, A<CancellationToken>._)).Returns(new StoredConnection(Profile, null));
        A.CallTo(() => Gateway.ListDatabasesAsync(A<MongoConnectionTarget>._, A<CancellationToken>._))
            .Returns(new[] { new DatabaseInfo("demo", false) });
        var factory = A.Fake<IMongoShellSessionFactory>();
        A.CallTo(() => factory.CreateAsync(A<MongoConnectionTarget>._, A<CancellationToken>._)).Returns(Shell);
        A.CallTo(() => Shell.IsRunning).Returns(true);
        _queries = new(factory, History, Gateway, new SettingsService(Settings), Loggers.CreateLogger<QueryService>());
        Workspace = new(_queries, Catalog, Limiter, Text, Loggers.CreateLogger<QueryWorkspace>());
    }

    public async ValueTask DisposeAsync()
    {
        await Workspace.DisposeAsync();
        await Catalog.DisposeAsync();
        await _queries.DisposeAsync();
        await _tunnels.DisposeAsync();
        Limiter.Dispose();
    }
}
