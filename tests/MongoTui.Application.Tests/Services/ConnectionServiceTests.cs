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
using MongoTui.Domain.Exceptions;
using MongoTui.Domain.Primitives;

namespace MongoTui.Application.Tests.Services;

public sealed class ConnectionServiceTests
{
    private readonly IConnectionRepository _repository = A.Fake<IConnectionRepository>();
    private readonly ICryptoService _crypto = A.Fake<ICryptoService>();
    private readonly SecretKeyService _secrets;
    private readonly IMongoGateway _gateway = A.Fake<IMongoGateway>();
    private readonly ISshTunnelFactory _tunnelFactory = A.Fake<ISshTunnelFactory>();
    private readonly ConnectionService _sut;

    public ConnectionServiceTests()
    {
        _secrets = new SecretKeyService(_crypto);
        _sut = new ConnectionService(_repository, _secrets, _gateway,
            new ConnectionTunnelService(_tunnelFactory, A.Fake<ITwoFactorPrompt>(), _gateway));
        A.CallTo(() => _repository.GetAsync(A<ConnectionId>._, A<CancellationToken>._))
            .Returns((StoredConnection?)null);
    }

    [Fact]
    public async Task Should_EncryptSecret_When_UnlockedAndSecretProvided()
    {
        await UnlockAsync();
        StoredConnection? saved = null;
        var nonce = new byte[] { 1, 2, 3 };
        A.CallTo(() => _crypto.Encrypt(A<byte[]>._, A<byte[]>._, out nonce)).Returns([9, 9]);
        A.CallTo(() => _repository.AddAsync(A<StoredConnection>._, A<CancellationToken>._))
            .Invokes((StoredConnection value, CancellationToken _) => saved = value);

        await _sut.SaveAsync(NewProfile(), "s3cr3t", CancellationToken.None);

        saved!.Secret.Should().NotBeNull();
        saved.Secret!.CipherText.Should().Equal(9, 9);
    }

    [Fact]
    public async Task ShouldThrow_SecretsLockedException_When_SavingSecretWhileLocked()
    {
        var act = async () => await _sut.SaveAsync(NewProfile(), "s3cr3t", CancellationToken.None);

        await act.Should().ThrowAsync<SecretsLockedException>();
    }

    [Fact]
    public async Task Should_ExtractUriCredentials_When_FieldsEmpty()
    {
        await UnlockAsync();
        StoredConnection? saved = null;
        var nonce = new byte[] { 1, 2, 3 };
        A.CallTo(() => _crypto.Encrypt(A<byte[]>._, A<byte[]>._, out nonce)).Returns([9, 9]);
        A.CallTo(() => _repository.AddAsync(A<StoredConnection>._, A<CancellationToken>._))
            .Invokes((StoredConnection value, CancellationToken _) => saved = value);
        var profile = ConnectionProfile.Create(
            ConnectionId.New(), "local", "mongodb://alice:s3cret@localhost:27017/mydb");

        await _sut.SaveAsync(profile, null, CancellationToken.None);

        var stored = saved!;
        stored.Profile.Uri.Should().Be("mongodb://localhost:27017/mydb");
        stored.Profile.Auth.Username.Should().Be("alice");
        stored.Profile.Auth.Kind.Should().Be(AuthKind.ScramSha256);
        stored.Profile.Auth.AuthDatabase.Should().Be("mydb");
        stored.Secret.Should().NotBeNull();
    }

    [Fact]
    public async Task Should_UpdateExisting_When_ProfileAlreadyStored()
    {
        var profile = NewProfile();
        A.CallTo(() => _repository.GetAsync(profile.Id, A<CancellationToken>._))
            .Returns(new StoredConnection(profile, null));

        await _sut.SaveAsync(profile, null, CancellationToken.None);

        A.CallTo(() => _repository.UpdateAsync(A<StoredConnection>._, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _repository.AddAsync(A<StoredConnection>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task Should_ResolveTargetWithCredentials_When_SecretStored()
    {
        await UnlockAsync();
        var profile = ConnectionProfile.Create(
            ConnectionId.New(),
            "secure",
            "mongodb://localhost:27017",
            new AuthOptions { Kind = AuthKind.ScramSha256, Username = "bob" });
        A.CallTo(() => _repository.GetAsync(profile.Id, A<CancellationToken>._))
            .Returns(new StoredConnection(profile, new EncryptedSecret(new byte[12], new byte[8])));
        A.CallTo(() => _crypto.Decrypt(A<byte[]>._, A<byte[]>._, A<byte[]>._))
            .Returns(Encoding.UTF8.GetBytes("pw"));

        var target = await _sut.ResolveTargetAsync(profile.Id, CancellationToken.None);

        target.ConnectionString.Should().Be("mongodb://bob:pw@localhost:27017");
    }

    [Fact]
    public void Should_ReturnBareUri_When_NoAuthentication()
    {
        var profile = NewProfile();

        var connectionString = ConnectionService.BuildConnectionString(profile, null);

        connectionString.Should().Be("mongodb://localhost:27017");
    }

    [Fact]
    public void Should_AppendAuthSource_When_AuthDatabaseProvided()
    {
        var profile = ConnectionProfile.Create(
            ConnectionId.New(),
            "secure",
            "mongodb://localhost:27017",
            new AuthOptions { Kind = AuthKind.ScramSha256, Username = "bob", AuthDatabase = "admin" });

        var connectionString = ConnectionService.BuildConnectionString(profile, "pw");

        connectionString.Should().Be("mongodb://bob:pw@localhost:27017?authSource=admin");
    }

    [Fact]
    public async Task Should_TestConnection_When_GatewaySucceeds()
    {
        var act = async () => await _sut.TestConnectionAsync(NewProfile(), null, CancellationToken.None);

        await act.Should().NotThrowAsync();
        A.CallTo(() => _gateway.ListDatabasesAsync(A<MongoConnectionTarget>._, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Should_TestViaProxyAndCloseTunnel_WhenSshEnabled(bool gatewayFails)
    {
        var session = A.Fake<ISshTunnelSession>();
        A.CallTo(() => session.LocalPort).Returns(1080);
        A.CallTo(() => session.DisposeAsync()).Returns(ValueTask.CompletedTask);
        A.CallTo(() => _tunnelFactory.StartAsync(A<SshTunnelOptions>._, A<ITwoFactorPrompt>._, A<CancellationToken>._))
            .Returns(session);
        var profile = NewProfile() with { Ssh = new SshTunnelOptions { Enabled = true, Host = "jump" } };
        if (gatewayFails)
        {
            A.CallTo(() => _gateway.ListDatabasesAsync(A<MongoConnectionTarget>._, A<CancellationToken>._))
                .Throws(new ConnectionFailedException("MongoDB refused the connection."));
        }

        var failure = await Record.ExceptionAsync(() => _sut.TestConnectionAsync(profile, null, CancellationToken.None));

        (failure is ConnectionFailedException).Should().Be(gatewayFails);
        A.CallTo(() => _gateway.ListDatabasesAsync(
            A<MongoConnectionTarget>.That.Matches(target => target.ConnectionString == "mongodb://localhost:27017/?proxyHost=127.0.0.1&proxyPort=1080"),
            A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        A.CallTo(() => session.DisposeAsync()).MustHaveHappenedOnceExactly();
        A.CallTo(() => _gateway.ReleaseClient(
            A<MongoConnectionTarget>.That.Matches(target => target.ConnectionString.Contains("proxyPort=1080"))))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task ShouldThrow_DomainException_When_ResolvingUnknownConnection()
    {
        A.CallTo(() => _repository.GetAsync(A<ConnectionId>._, A<CancellationToken>._))
            .Returns((StoredConnection?)null);

        var act = async () => await _sut.ResolveTargetAsync(ConnectionId.New(), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>();
    }

    private Task UnlockAsync()
    {
        _secrets.SetKek(new byte[32]);
        return Task.CompletedTask;
    }

    private static ConnectionProfile NewProfile() =>
        ConnectionProfile.Create(ConnectionId.New(), "local", "mongodb://localhost:27017");
}
