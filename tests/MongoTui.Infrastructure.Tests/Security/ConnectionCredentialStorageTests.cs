using AwesomeAssertions;
using MongoTui.Application.Models;
using MongoTui.Domain.Connections;
using MongoTui.Domain.Exceptions;
using MongoTui.Infrastructure.Tests.Fakes;

namespace MongoTui.Infrastructure.Tests.Security;

public sealed class ConnectionCredentialStorageTests
{
    [Theory]
    [InlineData("mongodb://alice:p%40ss%3Aword@localhost/app?authSource=admin")]
    [InlineData("mongodb+srv://alice:p%40ss%3Aword@cluster.example/app?authSource=admin")]
    [InlineData("mongodb://alice:p%40ss%3Aword@localhost/?authSource=admin")]
    public async Task Should_EncryptUriPassword_When_SavingFullUri(string uri)
    {
        await using var fixture = new SecretStorageFixture();
        await fixture.Connections.UnlockAsync("master", CancellationToken.None);
        var profile = ConnectionProfile.Create(fixture.Id, "test", uri);

        await fixture.Connections.SaveAsync(profile, null, CancellationToken.None);

        var stored = await fixture.Repository.GetAsync(profile.Id, CancellationToken.None);
        stored!.Profile.Uri.Should().NotContain("@").And.NotContain("p%40ss");
        stored.Profile.Auth.Username.Should().Be("alice");
        stored.Profile.Auth.AuthDatabase.Should().Be("admin");
        fixture.Secrets.Unprotect(stored.Secret!).Should().Be("p@ss:word");
    }

    [Fact]
    public async Task Should_MigrateLegacyCredentials_When_SecretsUnlocked()
    {
        await using var fixture = new SecretStorageFixture();
        var profile = ConnectionProfile.Create(fixture.Id, "legacy", "mongodb://alice:password@localhost/app");
        await fixture.Repository.AddAsync(new StoredConnection(profile, null), CancellationToken.None);

        await fixture.Connections.UnlockAsync("master", CancellationToken.None);

        var stored = await fixture.Repository.GetAsync(profile.Id, CancellationToken.None);
        stored!.Profile.Uri.Should().Be("mongodb://localhost/app");
        fixture.Secrets.Unprotect(stored.Secret!).Should().Be("password");
        (await fixture.Connections.ResolveTargetAsync(profile.Id, CancellationToken.None))
            .ConnectionString.Should().Be("mongodb://alice:password@localhost/app?authSource=app");
    }

    [Fact]
    public async Task ShouldNot_PersistPlaintext_When_SavingFullUriWhileLocked()
    {
        await using var fixture = new SecretStorageFixture();
        var profile = ConnectionProfile.Create(fixture.Id, "test", "mongodb://alice:password@localhost");

        var act = () => fixture.Connections.SaveAsync(profile, null, CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>();
        (await fixture.Repository.GetAllAsync(CancellationToken.None)).Should().BeEmpty();
    }

    [Fact]
    public async Task Should_MigrateLegacyCredentials_When_AutomaticUnlockSucceeds()
    {
        await using var fixture = new SecretStorageFixture();
        await fixture.Connections.UnlockAsync("master", CancellationToken.None);
        var profile = ConnectionProfile.Create(fixture.Id, "legacy", "mongodb://alice:password@localhost/app");
        await fixture.Repository.AddAsync(new StoredConnection(profile, null), CancellationToken.None);

        var unlocked = await fixture.Connections.TryAutoUnlockAsync(CancellationToken.None);

        unlocked.Should().BeTrue();
        var stored = await fixture.Repository.GetAsync(profile.Id, CancellationToken.None);
        stored!.Profile.Uri.Should().NotContain("password");
        fixture.Secrets.Unprotect(stored.Secret!).Should().Be("password");
    }
}
