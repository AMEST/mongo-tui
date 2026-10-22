using AwesomeAssertions;
using FakeItEasy;
using MongoTui.Application.Abstractions.Security;
using MongoTui.Application.Models;
using MongoTui.Application.Services;
using MongoTui.Domain.Connections;
using MongoTui.Domain.Primitives;
using MongoTui.Infrastructure.Tests.Fakes;

namespace MongoTui.Infrastructure.Tests.Security;

public sealed class SecurityAutoUnlockTests
{
    [Theory]
    [InlineData(SecurityMode.None, false)]
    [InlineData(SecurityMode.None, true)]
    [InlineData(SecurityMode.Keyring, false)]
    [InlineData(SecurityMode.Keyring, true)]
    public async Task Should_KeepSecretsLocked_When_CandidateCannotDecryptStoredSecret(SecurityMode mode, bool apiKeyOnly)
    {
        await using var fixture = new SecretStorageFixture();
        await fixture.InitializeAsync();
        if (apiKeyOnly) await fixture.Connections.ClearSecretsAsync(CancellationToken.None);
        else await fixture.Ai.ClearApiKeyAsync(CancellationToken.None);
        var legacy = ConnectionProfile.Create(ConnectionId.New(), "legacy", "mongodb://alice:password@localhost");
        await fixture.Repository.AddAsync(new StoredConnection(legacy, null), CancellationToken.None);
        var security = await CreateSecurityAsync(fixture, mode, new byte[32]);
        var before = await fixture.Repository.GetAllAsync(CancellationToken.None);
        var settings = await fixture.Settings.GetAllAsync(CancellationToken.None);

        var result = await security.TryAutoUnlockAsync(CancellationToken.None);

        result.Should().Be(AutoUnlockResult.KeyMaterialUnavailable);
        security.IsUnlocked.Should().BeFalse();
        (await fixture.Repository.GetAllAsync(CancellationToken.None)).Should().BeEquivalentTo(before);
        (await fixture.Settings.GetAllAsync(CancellationToken.None)).Should().BeEquivalentTo(settings);
    }

    [Theory]
    [InlineData(SecurityMode.None)]
    [InlineData(SecurityMode.Keyring)]
    public async Task Should_UnlockAndMigrateCredentials_When_CandidateDecryptsStoredSecrets(SecurityMode mode)
    {
        await using var fixture = new SecretStorageFixture();
        await fixture.InitializeAsync();
        var kek = await fixture.PasswordSource.UnlockAsync("old-master", CancellationToken.None);
        var legacy = ConnectionProfile.Create(ConnectionId.New(), "legacy", "mongodb://alice:password@localhost");
        await fixture.Repository.AddAsync(new StoredConnection(legacy, null), CancellationToken.None);
        var security = await CreateSecurityAsync(fixture, mode, kek);

        var result = await security.TryAutoUnlockAsync(CancellationToken.None);

        result.Should().Be(AutoUnlockResult.Unlocked);
        security.IsUnlocked.Should().BeTrue();
        (await fixture.Connections.GetSecretAsync(fixture.Id, CancellationToken.None)).Should().Be("first-secret");
        (await fixture.Ai.GetAsync(CancellationToken.None)).ApiKey.Should().Be("api-secret");
        var migrated = await fixture.Repository.GetAsync(legacy.Id, CancellationToken.None);
        migrated!.Profile.Uri.Should().Be("mongodb://localhost");
        fixture.Secrets.Unprotect(migrated.Secret!).Should().Be("password");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Should_KeepSecretsLocked_When_OnlyOneStoredSecretUsesAnotherKey(bool apiKey)
    {
        await using var fixture = new SecretStorageFixture();
        await fixture.InitializeAsync();
        var kek = await fixture.PasswordSource.UnlockAsync("old-master", CancellationToken.None);
        fixture.Secrets.SetKek(new byte[32]);
        if (apiKey) await fixture.Ai.SaveAsync("https://api.test/v1", "model", "other-key", CancellationToken.None);
        else await fixture.Connections.SaveAsync(ConnectionProfile.Create(ConnectionId.New(), "other", "mongodb://localhost"),
            "other-password", CancellationToken.None);
        var security = await CreateSecurityAsync(fixture, SecurityMode.None, kek);

        var result = await security.TryAutoUnlockAsync(CancellationToken.None);

        result.Should().Be(AutoUnlockResult.KeyMaterialUnavailable);
        security.IsUnlocked.Should().BeFalse();
    }

    private async Task<SecurityModeService> CreateSecurityAsync(
        SecretStorageFixture fixture, SecurityMode mode, byte[] kek)
    {
        var source = A.Fake<IKekSource>();
        A.CallTo(() => source.Mode).Returns(mode);
        A.CallTo(() => source.IsAvailable).Returns(true);
        A.CallTo(() => source.HasMaterialAsync(A<CancellationToken>._)).Returns(Task.FromResult(true));
        A.CallTo(() => source.TryGetKekAsync(A<CancellationToken>._)).Returns(Task.FromResult<byte[]?>(kek));
        await fixture.Settings.SetAsync(SecurityModeService.ModeSettingKey,
            mode == SecurityMode.None ? "none" : "keyring", CancellationToken.None);
        fixture.Secrets.Lock();
        return fixture.CreateSecurity(fixture.Secrets, source);
    }
}
