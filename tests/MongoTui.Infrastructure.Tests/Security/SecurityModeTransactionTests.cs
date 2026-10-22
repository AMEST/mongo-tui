using AwesomeAssertions;
using MongoTui.Domain.Connections;
using MongoTui.Domain.Exceptions;
using MongoTui.Infrastructure.Tests.Fakes;

namespace MongoTui.Infrastructure.Tests.Security;

public sealed class SecurityModeTransactionTests
{
    [Theory]
    [InlineData("security.kek_salt")]
    [InlineData("security.kek_check")]
    [InlineData("ai.api_key")]
    public async Task Should_RetainOldPasswordAndSecrets_When_RotationWriteFails(string failedKey)
    {
        await using var fixture = new SecretStorageFixture();
        await fixture.InitializeAsync();
        var before = await fixture.Settings.GetAllAsync(CancellationToken.None);
        fixture.BeforeWriteAsync = key => key == failedKey
            ? Task.FromException(new IOException("Injected storage failure.")) : Task.CompletedTask;

        var act = () => fixture.Security.ChangePasswordAsync("new-master", CancellationToken.None);

        await act.Should().ThrowAsync<IOException>();
        (await fixture.Settings.GetAllAsync(CancellationToken.None)).Should().BeEquivalentTo(before);
        (await fixture.Connections.GetSecretAsync(fixture.Id, CancellationToken.None)).Should().Be("first-secret");
        var restarted = await fixture.UnlockAsync("old-master");
        foreach (var stored in await fixture.Repository.GetAllAsync(CancellationToken.None))
            restarted.Unprotect(stored.Secret!).Should().BeOneOf("first-secret", "second-secret");
    }

    [Fact]
    public async Task Should_RollBackEverySecret_When_CancelledAfterWrites()
    {
        await using var fixture = new SecretStorageFixture();
        await fixture.InitializeAsync();
        var before = await fixture.Settings.GetAllAsync(CancellationToken.None);
        using var cancellation = new CancellationTokenSource();
        fixture.CancelAfterWrite = cancellation;

        var act = () => fixture.Security.ChangePasswordAsync("new-master", cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        (await fixture.Settings.GetAllAsync(CancellationToken.None)).Should().BeEquivalentTo(before);
        (await fixture.Connections.GetSecretAsync(fixture.Id, CancellationToken.None)).Should().Be("first-secret");
        (await fixture.Ai.GetAsync(CancellationToken.None)).ApiKey.Should().Be("api-secret");
        await fixture.UnlockAsync("old-master");
    }

    [Fact]
    public async Task Should_UnlockEverySecretAfterRestart_When_RotationSucceeds()
    {
        await using var fixture = new SecretStorageFixture();
        await fixture.InitializeAsync();

        await fixture.Security.ChangePasswordAsync("new-master", CancellationToken.None);

        var restarted = await fixture.UnlockAsync("new-master");
        foreach (var stored in await fixture.Repository.GetAllAsync(CancellationToken.None))
            restarted.Unprotect(stored.Secret!).Should().BeOneOf("first-secret", "second-secret");
        (await fixture.Ai.GetAsync(CancellationToken.None)).ApiKey.Should().Be("api-secret");
        var oldPassword = () => fixture.UnlockAsync("old-master");
        await oldPassword.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task Should_SaveWithNewKey_When_SaveOverlapsRotation()
    {
        await using var fixture = new SecretStorageFixture();
        await fixture.InitializeAsync();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.BeforeWriteAsync = async key =>
        {
            if (key != "security.kek_salt") return;
            entered.SetResult();
            await release.Task;
        };

        await RotateAndSaveAsync(fixture, entered, release);

        var restarted = await fixture.UnlockAsync("new-master");
        var stored = await fixture.Repository.GetAsync(fixture.Id, CancellationToken.None);
        restarted.Unprotect(stored!.Secret!).Should().Be("updated-secret");
    }

    private static async Task RotateAndSaveAsync(SecretStorageFixture fixture, TaskCompletionSource entered, TaskCompletionSource release)
    {
        var rotation = fixture.Security.ChangePasswordAsync("new-master", CancellationToken.None);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var save = fixture.Connections.SaveAsync(
            ConnectionProfile.Create(fixture.Id, "first", "mongodb://localhost"), "updated-secret", CancellationToken.None);
        release.SetResult();
        await Task.WhenAll(rotation, save).WaitAsync(TimeSpan.FromSeconds(10));
    }
}
