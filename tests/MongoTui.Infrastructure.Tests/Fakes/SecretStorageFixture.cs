using FakeItEasy;
using Microsoft.Data.Sqlite;
using MongoTui.Application.Abstractions.Mongo;
using MongoTui.Application.Abstractions.Persistence;
using MongoTui.Application.Abstractions.Security;
using MongoTui.Application.Abstractions.Ssh;
using MongoTui.Application.Models;
using MongoTui.Application.Services;
using MongoTui.Domain.Connections;
using MongoTui.Domain.Primitives;
using MongoTui.Infrastructure.Persistence.Sqlite;
using MongoTui.Infrastructure.Security;

namespace MongoTui.Infrastructure.Tests.Fakes;

internal sealed class SecretStorageFixture : IAsyncDisposable
{
    private static readonly Argon2Parameters FastParameters = new(MemorySizeKb: 1024, Iterations: 1, DegreesOfParallelism: 1);

    private readonly string _path = Path.Combine(Path.GetTempPath(), $"mongotui-security-{Guid.NewGuid():N}.db");
    private readonly SqliteSettingsStore _settings;
    private readonly AesGcmCryptoService _crypto = new();

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
        A.CallTo(() => Settings.DeleteAsync(A<string>._, A<CancellationToken>._))
            .ReturnsLazily((string key, CancellationToken token) => _settings.DeleteAsync(key, token));
        Repository = new SqliteConnectionRepository(Database);
        Secrets = CreateSecrets();
        var gateway = A.Fake<IMongoGateway>();
        Connections = new ConnectionService(Repository, Secrets, gateway,
            new ConnectionTunnelService(A.Fake<ISshTunnelFactory>(), A.Fake<ITwoFactorPrompt>(), gateway));
        Ai = new AiSettingsService(Settings, Secrets);
        PasswordSource = new PasswordKekSource(Settings, _crypto, FastParameters);
        Security = CreateSecurity(Secrets);
    }

    public SqliteDatabase Database { get; }
    public SqliteConnectionRepository Repository { get; }
    public ISettingsStore Settings { get; }
    public SecretKeyService Secrets { get; }
    public ConnectionService Connections { get; }
    public AiSettingsService Ai { get; }
    public PasswordKekSource PasswordSource { get; }
    public SecurityModeService Security { get; }
    public ConnectionId Id { get; } = ConnectionId.New();
    public Func<string, Task>? BeforeWriteAsync { get; set; }
    public CancellationTokenSource? CancelAfterWrite { get; set; }

    public SecretKeyService CreateSecrets() => new(_crypto);

    public SecurityModeService CreateSecurity(SecretKeyService secrets, params IKekSource[] additionalSources)
    {
        var gateway = A.Fake<IMongoGateway>();
        var connections = new ConnectionService(Repository, secrets, gateway,
            new ConnectionTunnelService(A.Fake<ISshTunnelFactory>(), A.Fake<ITwoFactorPrompt>(), gateway));
        var ai = new AiSettingsService(Settings, secrets);
        var source = new PasswordKekSource(Settings, _crypto, FastParameters);
        IKekSource[] sources = [source, .. additionalSources];
        var detector = new SecurityModeDetector(Settings, Repository, sources, ai);

        return new SecurityModeService(Settings, sources, secrets, connections, ai, Database, detector);
    }

    public async Task<SecretKeyService> UnlockAsync(string password)
    {
        var secrets = CreateSecrets();
        var security = CreateSecurity(secrets);
        await security.DetectAsync(CancellationToken.None);
        await security.UnlockWithPasswordAsync(password, CancellationToken.None);

        return secrets;
    }

    public async Task InitializeAsync()
    {
        await Security.DetectAsync(CancellationToken.None);
        await Security.InitializeAsync(SecurityMode.Password, "old-master", CancellationToken.None);
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
