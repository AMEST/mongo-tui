using FakeItEasy;
using Microsoft.Data.Sqlite;
using MongoTui.Application.Abstractions.Mongo;
using MongoTui.Application.Abstractions.Persistence;
using MongoTui.Application.Abstractions.Ssh;
using MongoTui.Application.Services;
using MongoTui.Domain.Connections;
using MongoTui.Domain.Primitives;
using MongoTui.Infrastructure.Persistence.Sqlite;
using MongoTui.Infrastructure.Security;

namespace MongoTui.Infrastructure.Tests.Fakes;

internal sealed class SecretStorageFixture : IAsyncDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"mongotui-security-{Guid.NewGuid():N}.db");
    private readonly SqliteSettingsStore _settings;

    public SecretStorageFixture()
    {
        Database = new SqliteDatabase(_path);
        Database.Migrate();
        _settings = new SqliteSettingsStore(Database);
        Settings = A.Fake<ISettingsStore>();
        A.CallTo(() => Settings.GetAsync(A<string>._, A<CancellationToken>._))
            .ReturnsLazily((string key, CancellationToken token) => _settings.GetAsync(key, token));
        A.CallTo(() => Settings.GetAllAsync(A<CancellationToken>._))
            .ReturnsLazily((CancellationToken token) => _settings.GetAllAsync(token));
        A.CallTo(() => Settings.SetAsync(A<string>._, A<string>._, A<CancellationToken>._))
            .ReturnsLazily((string key, string value, CancellationToken token) => SetAsync(key, value, token));
        Repository = new SqliteConnectionRepository(Database);
        Secrets = CreateSecrets();
        var gateway = A.Fake<IMongoGateway>();
        Connections = new ConnectionService(Repository, Secrets, gateway,
            new ConnectionTunnelService(A.Fake<ISshTunnelFactory>(), A.Fake<ITwoFactorPrompt>(), gateway));
        Ai = new AiSettingsService(Settings, Secrets);
        Master = new MasterPasswordService(Secrets, Connections, Ai, Database);
    }

    public SqliteDatabase Database { get; }
    public SqliteConnectionRepository Repository { get; }
    public ISettingsStore Settings { get; }
    public SecretKeyService Secrets { get; }
    public ConnectionService Connections { get; }
    public AiSettingsService Ai { get; }
    public MasterPasswordService Master { get; }
    public ConnectionId Id { get; } = ConnectionId.New();
    public Func<string, Task>? BeforeWriteAsync { get; set; }
    public CancellationTokenSource? CancelAfterWrite { get; set; }

    public SecretKeyService CreateSecrets() => new(new MasterPasswordKeyProvider(Settings,
        new AesGcmCryptoService(), new Argon2Parameters(1024, 1, 1)), new AesGcmCryptoService());

    public async Task InitializeAsync()
    {
        await Connections.UnlockAsync("old-master", CancellationToken.None);
        await Connections.SaveAsync(ConnectionProfile.Create(Id, "first", "mongodb://localhost"), "first-secret", CancellationToken.None);
        await Connections.SaveAsync(ConnectionProfile.Create(ConnectionId.New(), "second", "mongodb://localhost"), "second-secret", CancellationToken.None);
        await Ai.SaveAsync("https://api.test/v1", "test-model", "api-secret", CancellationToken.None);
    }

    private async Task SetAsync(string key, string value, CancellationToken cancellationToken)
    {
        if (BeforeWriteAsync is not null) await BeforeWriteAsync(key);
        await _settings.SetAsync(key, value, cancellationToken);
        if (key == "ai.api_key") CancelAfterWrite?.Cancel();
    }

    public ValueTask DisposeAsync()
    {
        SqliteConnection.ClearAllPools();
        File.Delete(_path);
        return ValueTask.CompletedTask;
    }
}
