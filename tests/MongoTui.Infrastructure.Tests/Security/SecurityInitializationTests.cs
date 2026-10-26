using AwesomeAssertions;
using MongoTui.Application.Models;
using MongoTui.Domain.Exceptions;
using MongoTui.Infrastructure.Tests.Fakes;

namespace MongoTui.Infrastructure.Tests.Security;

public sealed class SecurityInitializationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Should_PreserveSecrets_WhenAnotherSessionRepeatsSetup(bool detectFirst)
    {
        await using var fixture = new SecretStorageFixture();
        var other = fixture.CreateSecurity(fixture.Secrets);
        if (detectFirst) await other.DetectAsync(CancellationToken.None);
        await fixture.InitializeAsync();
        var before = await fixture.Settings.GetAllAsync(CancellationToken.None);

        var initialize = () => other.InitializeAsync(SecurityMode.Password, "second-master", CancellationToken.None);

        await initialize.Should().ThrowAsync<DomainException>();
        other.CurrentMode.Should().Be(SecurityMode.Password);
        (await fixture.Settings.GetAllAsync(CancellationToken.None)).Should().BeEquivalentTo(before);
        (await fixture.Connections.GetSecretAsync(fixture.Id, CancellationToken.None)).Should().Be("first-secret");
        (await fixture.Ai.GetAsync(CancellationToken.None)).ApiKey.Should().Be("api-secret");
        var restarted = await fixture.UnlockAsync("old-master");
        var stored = await fixture.Repository.GetAsync(fixture.Id, CancellationToken.None);
        restarted.Unprotect(stored!.Secret!).Should().Be("first-secret");
    }

    [Fact]
    public async Task Should_RejectSetup_WhenPersistedSecurityStateIsInvalid()
    {
        await using var fixture = new SecretStorageFixture();
        await fixture.InitializeAsync();
        await fixture.Settings.SetAsync("security.kek_mode", "unknown", CancellationToken.None);
        var other = fixture.CreateSecurity(fixture.Secrets);
        var before = await fixture.Settings.GetAllAsync(CancellationToken.None);

        var initialize = () => other.InitializeAsync(SecurityMode.Password, "second-master", CancellationToken.None);

        await initialize.Should().ThrowAsync<DomainException>();
        (await fixture.Settings.GetAllAsync(CancellationToken.None)).Should().BeEquivalentTo(before);
        (await fixture.Connections.GetSecretAsync(fixture.Id, CancellationToken.None)).Should().Be("first-secret");
    }

    [Fact]
    public async Task Should_InitializeOnlyOnce_WhenSessionsSubmitConcurrently()
    {
        await using var fixture = new SecretStorageFixture();
        var other = fixture.CreateSecurity(fixture.Secrets);
        await other.DetectAsync(CancellationToken.None);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.BeforeWriteAsync = async key =>
        {
            if (key != "security.kek_salt") return;
            entered.TrySetResult();
            await release.Task.ConfigureAwait(false);
        };

        var first = fixture.Security.InitializeAsync(SecurityMode.Password, "old-master", CancellationToken.None);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var second = other.InitializeAsync(SecurityMode.Password, "second-master", CancellationToken.None);
        release.SetResult();
        await first.WaitAsync(TimeSpan.FromSeconds(5));

        var initialize = () => second.WaitAsync(TimeSpan.FromSeconds(5));
        await initialize.Should().ThrowAsync<DomainException>();
        await fixture.UnlockAsync("old-master");
    }
}
