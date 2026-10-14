using AwesomeAssertions;
using MongoTui.Application.Models;
using MongoTui.Domain.Connections;
using MongoTui.Domain.Primitives;
using MongoTui.Infrastructure.Persistence.Sqlite;

namespace MongoTui.Infrastructure.Tests.Persistence;

public sealed class SqliteConnectionRepositoryTests : IDisposable
{
    private readonly string _databasePath =
        Path.Combine(Path.GetTempPath(), $"mongotui-{Guid.NewGuid():N}.db");

    private readonly SqliteConnectionRepository _sut;

    public SqliteConnectionRepositoryTests()
    {
        var database = new SqliteDatabase(_databasePath);
        database.Migrate();
        _sut = new SqliteConnectionRepository(database);
    }

    public void Dispose()
    {
        if (File.Exists(_databasePath))
        {
            File.Delete(_databasePath);
        }
    }

    [Fact]
    public async Task Should_ReturnAllConnections_When_Multiple()
    {
        await _sut.AddAsync(NewConnection("local"), CancellationToken.None);
        await _sut.AddAsync(NewConnection("prod"), CancellationToken.None);

        var all = await _sut.GetAllAsync(CancellationToken.None);

        all.Should().HaveCount(2);
    }

    [Fact]
    public async Task Should_ReturnNull_When_Missing()
    {
        var found = await _sut.GetAsync(ConnectionId.New(), CancellationToken.None);

        found.Should().BeNull();
    }

    [Fact]
    public async Task Should_RoundTripSecret_When_SecretProvided()
    {
        var secret = new EncryptedSecret(new byte[12], [1, 2, 3, 4, 5, 6, 7, 8]);
        var stored = NewConnection("secure") with { Secret = secret };

        await _sut.AddAsync(stored, CancellationToken.None);
        var found = await _sut.GetAsync(stored.Profile.Id, CancellationToken.None);

        found!.Secret.Should().NotBeNull();
        found.Secret!.Nonce.Should().Equal(secret.Nonce);
        found.Secret.CipherText.Should().Equal(secret.CipherText);
    }

    [Fact]
    public async Task Should_ReturnNullSecret_When_NoneProvided()
    {
        var stored = NewConnection("plain");

        await _sut.AddAsync(stored, CancellationToken.None);
        var found = await _sut.GetAsync(stored.Profile.Id, CancellationToken.None);

        found!.Secret.Should().BeNull();
    }

    [Fact]
    public async Task Should_UpdateConnection_When_ProfileChanged()
    {
        var stored = NewConnection("local");
        await _sut.AddAsync(stored, CancellationToken.None);

        var updated = stored with { Profile = stored.Profile with { Name = "renamed" } };
        await _sut.UpdateAsync(updated, CancellationToken.None);
        var found = await _sut.GetAsync(stored.Profile.Id, CancellationToken.None);

        found!.Profile.Name.Should().Be("renamed");
    }

    [Fact]
    public async Task Should_DeleteConnection_When_Exists()
    {
        var stored = NewConnection("local");
        await _sut.AddAsync(stored, CancellationToken.None);

        await _sut.DeleteAsync(stored.Profile.Id, CancellationToken.None);
        var found = await _sut.GetAsync(stored.Profile.Id, CancellationToken.None);

        found.Should().BeNull();
    }

    [Fact]
    public async Task Should_PreserveOptions_When_Roundtrip()
    {
        var stored = new StoredConnection(
            ConnectionProfile.Create(
                ConnectionId.New(),
                "opts",
                "mongodb://localhost:27017",
                new AuthOptions { Kind = AuthKind.ScramSha256, Username = "admin", AuthDatabase = "admin" },
                new TlsOptions { Enabled = true, AllowInvalidCertificates = true },
                new SshTunnelOptions
                {
                    Enabled = true,
                    Host = "jump",
                    Port = 2222,
                    Username = "bob",
                    LocalPort = 1080,
                    PrivateKeyFile = "/keys/id_ed25519"
                }),
            Secret: null);

        await _sut.AddAsync(stored, CancellationToken.None);
        var found = await _sut.GetAsync(stored.Profile.Id, CancellationToken.None);

        found!.Profile.Auth.Kind.Should().Be(AuthKind.ScramSha256);
        found.Profile.Auth.Username.Should().Be("admin");
        found.Profile.Tls.Enabled.Should().BeTrue();
        found.Profile.Tls.AllowInvalidCertificates.Should().BeTrue();
        found.Profile.Ssh.Port.Should().Be(2222);
        found.Profile.Ssh.Username.Should().Be("bob");
        found.Profile.Ssh.LocalPort.Should().Be(1080);
        found.Profile.Ssh.PrivateKeyFile.Should().Be("/keys/id_ed25519");
    }

    private static StoredConnection NewConnection(string name) =>
        new(ConnectionProfile.Create(ConnectionId.New(), name, "mongodb://localhost:27017"), Secret: null);
}
