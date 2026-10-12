using AwesomeAssertions;
using MongoTui.Infrastructure.Security;
using MongoTui.Infrastructure.Tests.Fakes;

namespace MongoTui.Infrastructure.Tests.Security;

public sealed class MasterPasswordKeyProviderTests
{
    private static readonly Argon2Parameters FastParameters = new(MemorySizeKb: 1024, Iterations: 1, DegreesOfParallelism: 1);

    [Fact]
    public async Task Should_Return32ByteKey_When_PasswordProvided()
    {
        var sut = new MasterPasswordKeyProvider(new InMemorySettingsStore(), new AesGcmCryptoService(), FastParameters);

        var key = await sut.UnlockWithMasterPasswordAsync("p@ssw0rd", CancellationToken.None);

        key.Should().HaveCount(32);
    }

    [Fact]
    public async Task Should_ReturnSameKey_When_SamePasswordAndSalt()
    {
        var store = new InMemorySettingsStore();
        var sut = new MasterPasswordKeyProvider(store, new AesGcmCryptoService(), FastParameters);

        var first = await sut.UnlockWithMasterPasswordAsync("p@ssw0rd", CancellationToken.None);
        var second = await sut.UnlockWithMasterPasswordAsync("p@ssw0rd", CancellationToken.None);

        second.Should().Equal(first);
    }

    [Fact]
    public async Task ShouldThrow_DomainException_When_DifferentPassword()
    {
        var store = new InMemorySettingsStore();
        var sut = new MasterPasswordKeyProvider(store, new AesGcmCryptoService(), FastParameters);

        await sut.UnlockWithMasterPasswordAsync("first", CancellationToken.None);

        var act = async () => await sut.UnlockWithMasterPasswordAsync("second", CancellationToken.None);

        await act.Should().ThrowAsync<MongoTui.Domain.Exceptions.DomainException>();
    }

    [Fact]
    public async Task Should_UnlockWithNewPassword_When_PasswordChanged()
    {
        var store = new InMemorySettingsStore();
        var sut = new MasterPasswordKeyProvider(store, new AesGcmCryptoService(), FastParameters);

        var newKey = await sut.ChangeMasterPasswordAsync("new-pass", CancellationToken.None);
        var unlocked = await sut.UnlockWithMasterPasswordAsync("new-pass", CancellationToken.None);

        unlocked.Should().Equal(newKey);
    }

    [Fact]
    public async Task Should_PersistSalt_When_FirstUnlock()
    {
        var store = new InMemorySettingsStore();
        var sut = new MasterPasswordKeyProvider(store, new AesGcmCryptoService(), FastParameters);

        await sut.UnlockWithMasterPasswordAsync("p@ssw0rd", CancellationToken.None);
        var settings = await store.GetAllAsync(CancellationToken.None);

        settings.Should().ContainKey(MasterPasswordKeyProvider.SaltSettingKey);
    }

    [Theory]
    [InlineData("")]
    public async Task ShouldThrow_ArgumentException_When_EmptyPassword(string password)
    {
        var sut = new MasterPasswordKeyProvider(new InMemorySettingsStore(), new AesGcmCryptoService(), FastParameters);

        var act = async () => await sut.UnlockWithMasterPasswordAsync(password, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public void Should_ReportKeyringUnavailable()
    {
        var sut = new MasterPasswordKeyProvider(new InMemorySettingsStore(), new AesGcmCryptoService(), FastParameters);

        sut.IsKeyringAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task ShouldThrow_NotSupportedException_When_GetOrCreateKek()
    {
        var sut = new MasterPasswordKeyProvider(new InMemorySettingsStore(), new AesGcmCryptoService(), FastParameters);

        var act = async () => await sut.GetOrCreateKekAsync(CancellationToken.None);

        await act.Should().ThrowAsync<NotSupportedException>();
    }
}
