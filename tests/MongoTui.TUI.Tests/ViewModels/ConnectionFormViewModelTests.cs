using Microsoft.Extensions.Logging.Abstractions;
using MongoTui.Application.Abstractions.Diagnostics;
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
using MongoTui.TUI.Localization;
using MongoTui.TUI.ViewModels;

namespace MongoTui.TUI.Tests.ViewModels;

public sealed class ConnectionFormViewModelTests
{
    private readonly IConnectionRepository _repository = A.Fake<IConnectionRepository>();
    private readonly ICryptoService _crypto = A.Fake<ICryptoService>();
    private readonly SecretKeyService _secrets;
    private readonly IMongoGateway _gateway = A.Fake<IMongoGateway>();
    private readonly ConnectionService _service;
    private readonly ConnectionFormViewModel _sut;

    public ConnectionFormViewModelTests()
    {
        A.CallTo(() => _repository.GetAsync(A<ConnectionId>._, A<CancellationToken>._))
            .Returns((StoredConnection?)null);

        _secrets = new SecretKeyService(_crypto);
        _service = new ConnectionService(_repository, _secrets, _gateway,
            new ConnectionTunnelService(A.Fake<ISshTunnelFactory>(), A.Fake<ITwoFactorPrompt>(), _gateway), NullLogger<ConnectionService>.Instance);
        _sut = new ConnectionFormViewModel(_service, new ResxLocalizer());
        _sut.LoadNew();
    }

    [Fact]
    public async Task Should_ReturnFalseAndSetError_When_NameEmpty()
    {
        _sut.Name = "  ";

        var result = await _sut.SaveAsync(CancellationToken.None);

        result.Should().BeFalse();
        _sut.ValidationError.Should().NotBeNullOrWhiteSpace();
        A.CallTo(() => _repository.AddAsync(A<StoredConnection>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("\t")]
    public async Task Should_TestConnectionWithoutSavingOrChangingName_WhenNameBlank(string name)
    {
        _sut.Name = name;
        _sut.Uri = "mongodb://bob:pw@localhost:27017/app?authSource=admin";

        var error = await _sut.TestAsync(CancellationToken.None);

        error.Should().BeNull();
        _sut.Name.Should().Be(name);
        _sut.IsNew.Should().BeTrue();
        A.CallTo(() => _gateway.ListDatabasesAsync(
            A<MongoConnectionTarget>.That.Matches(target =>
                target.ConnectionString == "mongodb://bob:pw@localhost:27017/app?authSource=admin"),
            A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _repository.AddAsync(A<StoredConnection>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Theory]
    [InlineData("")]
    [InlineData("http://localhost")]
    public async Task Should_ValidateConnectionString_WhenTestingWithoutName(string uri)
    {
        _sut.Uri = uri;

        var error = await _sut.TestAsync(CancellationToken.None);

        error.Should().StartWith("Connection string");
        A.CallTo(() => _gateway.ListDatabasesAsync(A<MongoConnectionTarget>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task Should_SaveAndClearIsNew_When_Valid()
    {
        _sut.Name = "local";
        _sut.Uri = "mongodb://localhost:27017";

        var result = await _sut.SaveAsync(CancellationToken.None);

        result.Should().BeTrue();
        _sut.IsNew.Should().BeFalse();
        A.CallTo(() => _repository.AddAsync(A<StoredConnection>._, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Should_ReturnErrorMessage_When_ConnectionTestFails()
    {
        _sut.Name = "local";
        A.CallTo(() => _gateway.ListDatabasesAsync(A<MongoConnectionTarget>._, A<CancellationToken>._))
            .Throws(new ConnectionFailedException("refused"));

        var error = await _sut.TestAsync(CancellationToken.None);

        error.Should().Contain("refused");
        _sut.ValidationError.Should().Contain("refused");
    }

    [Fact]
    public void Should_LoadProfileIntoFields_When_EditingExisting()
    {
        var profile = ConnectionProfile.Create(
            ConnectionId.New(),
            "prod",
            "mongodb://host:27017",
            new AuthOptions { Kind = AuthKind.ScramSha256, Username = "admin" });

        _sut.Load(profile, "secret");

        _sut.IsNew.Should().BeFalse();
        _sut.Name.Should().Be("prod");
        _sut.AuthKind.Should().Be(AuthKind.ScramSha256);
        _sut.Username.Should().Be("admin");
        _sut.Password.Should().Be("secret");
    }

    [Fact]
    public async Task Should_PreserveSshAndTlsSettings_WhenEditingAndSavingProfile()
    {
        var ssh = new SshTunnelOptions
        {
            Enabled = true,
            Host = "tpam",
            Port = 4422,
            Username = "requester+user+system",
            LocalPort = 1080,
            PrivateKeyFile = "/keys/key"
        };
        var tls = new TlsOptions { Enabled = true };
        var profile = ConnectionProfile.Create(ConnectionId.New(), "prod", "mongodb://host", tls: tls, ssh: ssh);
        _sut.Load(profile, null);

        var saved = await _sut.SaveAsync(CancellationToken.None);

        saved.Should().BeTrue();
        A.CallTo(() => _repository.AddAsync(
            A<StoredConnection>.That.Matches(value => value.Profile.Ssh == ssh && value.Profile.Tls == tls),
            A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public void Should_ResetSshSettings_WhenStartingNewProfile()
    {
        _sut.Ssh = new SshTunnelOptions { Enabled = true, Host = "jump" };

        _sut.LoadNew();

        _sut.Ssh.Should().Be(new SshTunnelOptions());
    }

    [Fact]
    public void Should_PopulateFields_When_ApplyUriWithCredentials()
    {
        var applied = _sut.ApplyUri("mongodb://bob:pw@localhost:27017/app?authSource=admin");

        applied.Should().BeTrue();
        _sut.Uri.Should().Be("mongodb://localhost:27017/app?authSource=admin");
        _sut.Username.Should().Be("bob");
        _sut.Password.Should().Be("pw");
        _sut.AuthDatabase.Should().Be("admin");
        _sut.AuthKind.Should().Be(AuthKind.ScramSha256);
        _sut.Name.Should().Be("localhost");
    }

    [Fact]
    public void Should_SetError_When_ApplyUriInvalid()
    {
        var applied = _sut.ApplyUri("http://localhost");

        applied.Should().BeFalse();
        _sut.ValidationError.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Should_FillEmptyFieldsFromUri_When_MergingCredentials()
    {
        _sut.Uri = "mongodb://bob:pw@localhost:27017/app?authSource=admin";

        _sut.MergeCredentialsFromUri();

        _sut.Uri.Should().Be("mongodb://localhost:27017/app?authSource=admin");
        _sut.Username.Should().Be("bob");
        _sut.Password.Should().Be("pw");
        _sut.AuthDatabase.Should().Be("admin");
        _sut.AuthKind.Should().Be(AuthKind.ScramSha256);
    }

    [Fact]
    public void Should_KeepTypedCredentials_When_MergingUriCredentials()
    {
        _sut.Username = "typed";
        _sut.Password = "typed-pw";
        _sut.Uri = "mongodb://bob:pw@localhost:27017";

        _sut.MergeCredentialsFromUri();

        _sut.Username.Should().Be("typed");
        _sut.Password.Should().Be("typed-pw");
        _sut.Uri.Should().Be("mongodb://localhost:27017");
        _sut.AuthKind.Should().Be(AuthKind.ScramSha256);
    }

    [Theory]
    [InlineData("typed", null, "typed", "pw")]
    [InlineData(null, "typed-pw", "bob", "typed-pw")]
    public void Should_FillMissingCredential_When_MergingUriWithPartiallyFilledFields(
        string? username, string? password, string expectedUsername, string expectedPassword)
    {
        _sut.Username = username;
        _sut.Password = password;
        _sut.Uri = "mongodb://bob:pw@localhost/app";

        _sut.MergeCredentialsFromUri();

        _sut.Uri.Should().Be("mongodb://localhost/app");
        _sut.Username.Should().Be(expectedUsername);
        _sut.Password.Should().Be(expectedPassword);
    }

    [Fact]
    public void Should_SeparateCredentials_When_LoadingLegacyProfile()
    {
        var profile = ConnectionProfile.Create(ConnectionId.New(), "legacy", "mongodb://bob:pw@localhost/app");

        _sut.Load(profile, null);

        _sut.Uri.Should().Be("mongodb://localhost/app");
        _sut.Username.Should().Be("bob");
        _sut.Password.Should().Be("pw");
    }

    [Fact]
    public void Should_DoNothing_When_UriHasNoCredentials()
    {
        _sut.Uri = "mongodb://localhost:27017";

        _sut.MergeCredentialsFromUri();

        _sut.Username.Should().BeNull();
        _sut.Password.Should().BeNull();
        _sut.Uri.Should().Be("mongodb://localhost:27017");
    }

    [Fact]
    public async Task Should_FillFieldsFromUri_When_SavingWithEmptyCredentials()
    {
        _secrets.SetKek(new byte[32]);
        var nonce = new byte[] { 1, 2, 3 };
        A.CallTo(() => _crypto.Encrypt(A<byte[]>._, A<byte[]>._, out nonce)).Returns([9, 9]);
        _sut.Name = "local";
        _sut.Uri = "mongodb://bob:pw@localhost:27017/app";

        var saved = await _sut.SaveAsync(CancellationToken.None);

        saved.Should().BeTrue();
        _sut.Uri.Should().Be("mongodb://localhost:27017/app");
        _sut.Username.Should().Be("bob");
        _sut.Password.Should().Be("pw");
        _sut.AuthKind.Should().Be(AuthKind.ScramSha256);
    }
}
