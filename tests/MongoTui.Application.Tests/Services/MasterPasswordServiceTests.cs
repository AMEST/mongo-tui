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

public sealed class MasterPasswordServiceTests
{
    private readonly IConnectionRepository _repository = A.Fake<IConnectionRepository>();
    private readonly IMongoGateway _gateway = A.Fake<IMongoGateway>();
    private readonly ICryptoService _crypto = A.Fake<ICryptoService>();
    private readonly IKeyProvider _keyProvider = A.Fake<IKeyProvider>();
    private readonly ISettingsStore _settings = A.Fake<ISettingsStore>();
    private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);
    private readonly SecretKeyService _secrets;
    private readonly AiSettingsService _ai;
    private readonly MasterPasswordService _sut;

    public MasterPasswordServiceTests()
    {
        _secrets = new SecretKeyService(_keyProvider, _crypto);
        var connections = new ConnectionService(_repository, _secrets, _gateway,
            new ConnectionTunnelService(A.Fake<ISshTunnelFactory>(), A.Fake<ITwoFactorPrompt>()));
        _ai = new AiSettingsService(_settings, _secrets);
        _sut = new MasterPasswordService(_secrets, connections, _ai);

        A.CallTo(() => _settings.GetAsync(A<string>._, A<CancellationToken>._))
            .ReturnsLazily((string key, CancellationToken _) =>
                Task.FromResult(_values.TryGetValue(key, out var value) ? value : null));
        A.CallTo(() => _settings.SetAsync(A<string>._, A<string>._, A<CancellationToken>._))
            .Invokes((string key, string value, CancellationToken _) => _values[key] = value)
            .Returns(Task.CompletedTask);

        var nonce = new byte[] { 1 };
        A.CallTo(() => _crypto.Encrypt(A<byte[]>._, A<byte[]>._, out nonce)).Returns([9, 9]);
        A.CallTo(() => _crypto.Decrypt(A<byte[]>._, A<byte[]>._, A<byte[]>._)).Returns(Encoding.UTF8.GetBytes("pw"));
        A.CallTo(() => _repository.GetAllAsync(A<CancellationToken>._)).Returns(new List<StoredConnection>());
        A.CallTo(() => _keyProvider.ChangeMasterPasswordAsync(A<string>._, A<CancellationToken>._)).Returns(new byte[32]);
    }

    [Fact]
    public async Task Should_ReEncryptConnectionAndAiSecrets_When_ChangingPassword()
    {
        await UnlockAsync();

        var profile = ConnectionProfile.Create(ConnectionId.New(), "local", "mongodb://localhost:27017");
        A.CallTo(() => _repository.GetAllAsync(A<CancellationToken>._))
            .Returns(new List<StoredConnection> { new(profile, new EncryptedSecret(new byte[12], new byte[8])) });
        await _ai.SaveAsync("https://api.test/v1", "gpt-4o-mini", "secret", CancellationToken.None);

        StoredConnection? updated = null;
        A.CallTo(() => _repository.UpdateAsync(A<StoredConnection>._, A<CancellationToken>._))
            .Invokes((StoredConnection value, CancellationToken _) => updated = value);
        Fake.ClearRecordedCalls(_settings);

        await _sut.ChangeAsync("new-master", CancellationToken.None);

        updated.Should().NotBeNull();
        updated!.Secret!.CipherText.Should().Equal(9, 9);
        A.CallTo(() => _settings.SetAsync("ai.api_key", A<string>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _keyProvider.ChangeMasterPasswordAsync("new-master", A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task ShouldThrow_DomainException_When_ChangingPasswordWhileLocked()
    {
        var act = async () => await _sut.ChangeAsync("new-master", CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>();
    }

    private async Task UnlockAsync()
    {
        A.CallTo(() => _keyProvider.UnlockWithMasterPasswordAsync("master", A<CancellationToken>._)).Returns(new byte[32]);
        await _secrets.UnlockAsync("master", CancellationToken.None);
    }
}
