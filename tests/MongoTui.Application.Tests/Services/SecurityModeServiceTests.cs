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

public sealed class SecurityModeServiceTests
{
    private readonly IConnectionRepository _repository = A.Fake<IConnectionRepository>();
    private readonly ICryptoService _crypto = A.Fake<ICryptoService>();
    private readonly ISettingsStore _settings = A.Fake<ISettingsStore>();
    private readonly IStorageTransaction _transaction = A.Fake<IStorageTransaction>();
    private readonly IKekSource _password = A.Fake<IKekSource>();
    private readonly IKekSource _keyring = A.Fake<IKekSource>();
    private readonly IKekSource _none = A.Fake<IKekSource>();
    private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);
    private readonly SecretKeyService _secrets;
    private readonly SecurityModeService _sut;

    public SecurityModeServiceTests()
    {
        _secrets = new SecretKeyService(_crypto);
        var gateway = A.Fake<IMongoGateway>();
        var connections = new ConnectionService(_repository, _secrets, gateway,
            new ConnectionTunnelService(A.Fake<ISshTunnelFactory>(), A.Fake<ITwoFactorPrompt>(), gateway));
        var ai = new AiSettingsService(_settings, _secrets);

        A.CallTo(() => _settings.GetAsync(A<string>._, A<CancellationToken>._))
            .ReturnsLazily((string key, CancellationToken _) =>
                Task.FromResult(_values.TryGetValue(key, out var value) ? value : null));
        A.CallTo(() => _settings.SetAsync(A<string>._, A<string>._, A<CancellationToken>._))
            .Invokes((string key, string value, CancellationToken _) => _values[key] = value)
            .Returns(Task.CompletedTask);
        A.CallTo(() => _settings.DeleteAsync(A<string>._, A<CancellationToken>._))
            .Invokes((string key, CancellationToken _) => _values.Remove(key))
            .Returns(Task.CompletedTask);
        A.CallTo(() => _transaction.ExecuteAsync(A<Func<CancellationToken, Task>>._, A<CancellationToken>._))
            .ReturnsLazily((Func<CancellationToken, Task> action, CancellationToken token) => action(token));
        A.CallTo(() => _repository.GetAllAsync(A<CancellationToken>._))
            .Returns(new List<StoredConnection>());

        A.CallTo(() => _password.Mode).Returns(SecurityMode.Password);
        A.CallTo(() => _keyring.Mode).Returns(SecurityMode.Keyring);
        A.CallTo(() => _none.Mode).Returns(SecurityMode.None);
        A.CallTo(() => _password.IsAvailable).Returns(true);
        A.CallTo(() => _keyring.IsAvailable).Returns(true);
        A.CallTo(() => _none.IsAvailable).Returns(true);
        A.CallTo(() => _password.HasMaterialAsync(A<CancellationToken>._)).Returns(Task.FromResult(false));
        A.CallTo(() => _keyring.HasMaterialAsync(A<CancellationToken>._)).Returns(Task.FromResult(false));
        A.CallTo(() => _none.HasMaterialAsync(A<CancellationToken>._)).Returns(Task.FromResult(false));
        A.CallTo(() => _password.TryGetKekAsync(A<CancellationToken>._)).Returns(Task.FromResult<byte[]?>(null));
        A.CallTo(() => _keyring.TryGetKekAsync(A<CancellationToken>._)).Returns(Task.FromResult<byte[]?>(null));
        A.CallTo(() => _none.TryGetKekAsync(A<CancellationToken>._)).Returns(Task.FromResult<byte[]?>(null));
        A.CallTo(() => _password.CreateAsync(A<string>._, A<CancellationToken>._)).Returns(Task.FromResult(new byte[32]));
        A.CallTo(() => _keyring.CreateAsync(A<string>._, A<CancellationToken>._)).Returns(Task.FromResult(new byte[32]));
        A.CallTo(() => _none.CreateAsync(A<string>._, A<CancellationToken>._)).Returns(Task.FromResult(new byte[32]));
        A.CallTo(() => _password.UnlockAsync(A<string>._, A<CancellationToken>._)).Returns(Task.FromResult(new byte[32]));

        var nonce = new byte[] { 1 };
        A.CallTo(() => _crypto.Encrypt(A<byte[]>._, A<byte[]>._, out nonce)).Returns([9, 9]);
        A.CallTo(() => _crypto.Decrypt(A<byte[]>._, A<byte[]>._, A<byte[]>._)).Returns(Encoding.UTF8.GetBytes("pw"));

        var detector = new SecurityModeDetector(_settings, _repository, [_password, _keyring, _none], ai);
        _sut = new SecurityModeService(_settings, [_password, _keyring, _none], _secrets, connections, ai,
            _transaction, detector);
    }

    [Fact]
    public async Task Should_DetectFirstRun_When_NoModeAndNoSecrets()
    {
        var detection = await _sut.DetectAsync(CancellationToken.None);

        detection.State.Should().Be(SecurityState.FirstRun);
        detection.Mode.Should().BeNull();
        _sut.CurrentMode.Should().BeNull();
    }

    [Fact]
    public async Task Should_DetectPassword_When_LegacySaltExists()
    {
        _values["security.kek_salt"] = "salt";
        A.CallTo(() => _password.HasMaterialAsync(A<CancellationToken>._)).Returns(Task.FromResult(true));

        var detection = await _sut.DetectAsync(CancellationToken.None);

        detection.State.Should().Be(SecurityState.Ready);
        detection.Mode.Should().Be(SecurityMode.Password);
        _sut.CurrentMode.Should().Be(SecurityMode.Password);
    }

    [Fact]
    public async Task Should_DetectKeyringUnavailable_When_ModeIsKeyring()
    {
        _values[SecurityModeService.ModeSettingKey] = "keyring";
        A.CallTo(() => _keyring.IsAvailable).Returns(false);

        var detection = await _sut.DetectAsync(CancellationToken.None);

        detection.State.Should().Be(SecurityState.Invalid);
        detection.Problem.Should().Be(SecurityProblem.KeyringUnavailable);
    }

    [Fact]
    public async Task Should_DetectKeyringUnavailable_When_SourceIsNotRegistered()
    {
        _values[SecurityModeService.ModeSettingKey] = "keyring";
        var detector = new SecurityModeDetector(
            _settings, _repository, [_password, _none], new AiSettingsService(_settings, _secrets));

        var detection = await detector.DetectAsync(CancellationToken.None);

        detection.State.Should().Be(SecurityState.Invalid);
        detection.Problem.Should().Be(SecurityProblem.KeyringUnavailable);
    }

    [Fact]
    public async Task Should_DetectKeyringUnavailable_When_ReadingMaterialFails()
    {
        _values[SecurityModeService.ModeSettingKey] = "keyring";
        A.CallTo(() => _keyring.HasMaterialAsync(A<CancellationToken>._))
            .ThrowsAsync(new DomainException("Keyring is locked."));

        var detection = await _sut.DetectAsync(CancellationToken.None);

        detection.State.Should().Be(SecurityState.Invalid);
        detection.Problem.Should().Be(SecurityProblem.KeyringUnavailable);
    }

    [Fact]
    public async Task Should_DetectMissingMaterial_When_SecretsExist()
    {
        _values[SecurityModeService.ModeSettingKey] = "none";
        A.CallTo(() => _repository.GetAllAsync(A<CancellationToken>._))
            .Returns(new List<StoredConnection>
            {
                new(ConnectionProfile.Create(ConnectionId.New(), "local", "mongodb://localhost"),
                    new EncryptedSecret(new byte[1], new byte[1])),
            });

        var detection = await _sut.DetectAsync(CancellationToken.None);

        detection.State.Should().Be(SecurityState.Invalid);
        detection.Problem.Should().Be(SecurityProblem.KekFileMissing);
    }

    [Fact]
    public async Task Should_InitializePasswordMode_When_FirstRun()
    {
        await _sut.DetectAsync(CancellationToken.None);

        await _sut.InitializeAsync(SecurityMode.Password, "master", CancellationToken.None);

        _values[SecurityModeService.ModeSettingKey].Should().Be("password");
        _sut.CurrentMode.Should().Be(SecurityMode.Password);
        _sut.IsUnlocked.Should().BeTrue();
        A.CallTo(() => _password.CreateAsync("master", A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task ShouldThrow_When_InitializingWithoutPassword()
    {
        var act = async () => await _sut.InitializeAsync(SecurityMode.Password, null, CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task Should_RequirePassword_When_ModeIsPassword()
    {
        await _sut.InitializeAsync(SecurityMode.Password, "master", CancellationToken.None);
        _secrets.Lock();

        var result = await _sut.TryAutoUnlockAsync(CancellationToken.None);

        result.Should().Be(AutoUnlockResult.PasswordRequired);
        _sut.IsUnlocked.Should().BeFalse();
    }

    [Fact]
    public async Task Should_AutoUnlockKeyring_When_KeyExists()
    {
        _values[SecurityModeService.ModeSettingKey] = "keyring";
        A.CallTo(() => _keyring.HasMaterialAsync(A<CancellationToken>._)).Returns(Task.FromResult(true));
        A.CallTo(() => _keyring.TryGetKekAsync(A<CancellationToken>._)).Returns(Task.FromResult<byte[]?>(new byte[32]));
        await _sut.DetectAsync(CancellationToken.None);

        var result = await _sut.TryAutoUnlockAsync(CancellationToken.None);

        result.Should().Be(AutoUnlockResult.Unlocked);
        _sut.IsUnlocked.Should().BeTrue();
    }

    [Fact]
    public async Task Should_NotCreateKeyringKey_When_SecretsExist()
    {
        _values[SecurityModeService.ModeSettingKey] = "keyring";
        A.CallTo(() => _repository.GetAllAsync(A<CancellationToken>._))
            .Returns(new List<StoredConnection>
            {
                new(ConnectionProfile.Create(ConnectionId.New(), "local", "mongodb://localhost"),
                    new EncryptedSecret(new byte[1], new byte[1])),
            });
        await _sut.DetectAsync(CancellationToken.None);

        var result = await _sut.TryAutoUnlockAsync(CancellationToken.None);

        result.Should().Be(AutoUnlockResult.KeyMaterialUnavailable);
        A.CallTo(() => _keyring.CreateAsync(A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task Should_ChangePassword_When_ModeIsPassword()
    {
        await _sut.InitializeAsync(SecurityMode.Password, "master", CancellationToken.None);
        var newKek = Enumerable.Repeat((byte)7, 32).ToArray();
        A.CallTo(() => _password.CreateAsync("new-master", A<CancellationToken>._)).Returns(Task.FromResult(newKek));

        await _sut.ChangePasswordAsync("new-master", CancellationToken.None);

        _sut.IsUnlocked.Should().BeTrue();
        A.CallTo(() => _password.CreateAsync("new-master", A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _transaction.ExecuteAsync(A<Func<CancellationToken, Task>>._, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Should_SwitchToNone_When_PasswordUnlocked()
    {
        await _sut.InitializeAsync(SecurityMode.Password, "master", CancellationToken.None);
        var newKek = Enumerable.Repeat((byte)5, 32).ToArray();
        A.CallTo(() => _none.CreateAsync(A<string>._, A<CancellationToken>._)).Returns(Task.FromResult(newKek));

        await _sut.SwitchModeAsync(SecurityMode.None, null, CancellationToken.None);

        _sut.CurrentMode.Should().Be(SecurityMode.None);
        _values[SecurityModeService.ModeSettingKey].Should().Be("none");
        A.CallTo(() => _password.DeleteAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Should_SwitchToKeyring_When_NoneUnlocked()
    {
        await _sut.InitializeAsync(SecurityMode.None, null, CancellationToken.None);

        await _sut.SwitchModeAsync(SecurityMode.Keyring, null, CancellationToken.None);

        _sut.CurrentMode.Should().Be(SecurityMode.Keyring);
        _values[SecurityModeService.ModeSettingKey].Should().Be("keyring");
        A.CallTo(() => _none.DeleteAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Should_SwitchToPassword_When_NoneUnlocked()
    {
        await _sut.InitializeAsync(SecurityMode.None, null, CancellationToken.None);
        var newKek = Enumerable.Repeat((byte)9, 32).ToArray();
        A.CallTo(() => _password.CreateAsync("new-master", A<CancellationToken>._))
            .Returns(Task.FromResult(newKek));

        await _sut.SwitchModeAsync(SecurityMode.Password, "new-master", CancellationToken.None);

        _sut.CurrentMode.Should().Be(SecurityMode.Password);
        _values[SecurityModeService.ModeSettingKey].Should().Be("password");
        A.CallTo(() => _none.DeleteAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Should_SwitchToNone_When_KeyringUnlocked()
    {
        await _sut.InitializeAsync(SecurityMode.Keyring, null, CancellationToken.None);
        var newKek = Enumerable.Repeat((byte)4, 32).ToArray();
        A.CallTo(() => _none.CreateAsync(null, A<CancellationToken>._)).Returns(Task.FromResult(newKek));

        await _sut.SwitchModeAsync(SecurityMode.None, null, CancellationToken.None);

        _sut.CurrentMode.Should().Be(SecurityMode.None);
        _values[SecurityModeService.ModeSettingKey].Should().Be("none");
        A.CallTo(() => _keyring.DeleteAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Should_ResetSecretsAndMode_When_Requested()
    {
        await _sut.InitializeAsync(SecurityMode.Password, "master", CancellationToken.None);
        var profile = ConnectionProfile.Create(ConnectionId.New(), "local", "mongodb://localhost");
        A.CallTo(() => _repository.GetAllAsync(A<CancellationToken>._))
            .Returns(new List<StoredConnection> { new(profile, new EncryptedSecret(new byte[1], new byte[1])) });

        await _sut.ResetAsync(CancellationToken.None);

        _sut.CurrentMode.Should().BeNull();
        _sut.IsUnlocked.Should().BeFalse();
        _values.Should().NotContainKey(SecurityModeService.ModeSettingKey);
        A.CallTo(() => _repository.UpdateAsync(
            A<StoredConnection>.That.Matches(connection => connection.Secret == null),
            A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _password.DeleteAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Should_UnlockWithPassword_When_LegacyMode()
    {
        _values["security.kek_salt"] = "salt";
        A.CallTo(() => _password.HasMaterialAsync(A<CancellationToken>._)).Returns(Task.FromResult(true));
        A.CallTo(() => _password.UnlockAsync("master", A<CancellationToken>._)).Returns(Task.FromResult(new byte[32]));
        await _sut.DetectAsync(CancellationToken.None);

        await _sut.UnlockWithPasswordAsync("master", CancellationToken.None);

        _sut.IsUnlocked.Should().BeTrue();
        _values[SecurityModeService.ModeSettingKey].Should().Be("password");
    }
}
