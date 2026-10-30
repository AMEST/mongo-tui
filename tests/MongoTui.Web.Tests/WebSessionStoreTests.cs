using AwesomeAssertions;
using MongoTui.Testing;
using MongoTui.Web.Services;

namespace MongoTui.Web.Tests;

public sealed class WebSessionStoreTests
{
    [Fact]
    public void Should_KeepSessionsIndependent_WhenOneIsRevoked()
    {
        var sessions = new WebSessionStore(new SessionClock());
        var first = sessions.Create("revision");
        var second = sessions.Create("revision");
        var firstId = sessions.Authenticate(first, "revision")!;

        sessions.Revoke(firstId);

        sessions.Authenticate(first, "revision").Should().BeNull();
        sessions.Authenticate(second, "revision").Should().NotBeNull().And.NotBe(second);
        second.Should().HaveLength(64).And.NotBe(first);
    }

    [Fact]
    public void Should_ExpireBothKindsOfToken_WhenTheirLifetimeEnds()
    {
        var clock = new SessionClock();
        var sessions = new WebSessionStore(clock);
        var launch = sessions.CreateLaunchToken("revision");
        var session = sessions.Create("revision");

        clock.Advance(WebSessionStore.LaunchLifetime);

        sessions.RedeemLaunchToken(launch, "revision").Should().BeNull();
        sessions.Authenticate(session, "revision").Should().NotBeNull();
        clock.Advance(WebSessionStore.SessionLifetime);
        sessions.Authenticate(session, "revision").Should().BeNull();
    }

    [Fact]
    public async Task Should_RedeemLaunchExactlyOnce_WhenClientsRace()
    {
        var sessions = new WebSessionStore(new SessionClock());
        var launch = sessions.CreateLaunchToken("revision");

        var results = await Task.WhenAll(Enumerable.Range(0, 8)
            .Select(_ => Task.Run(() => sessions.RedeemLaunchToken(launch, "revision"))));

        results.Where(result => result is not null).Should().ContainSingle();
        sessions.Authenticate(results.Single(result => result is not null), "revision").Should().NotBeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-token")]
    public void Should_RejectMalformedTokenWithoutConsumingLaunch_WhenUnknownClientAttemptsEntry(string? invalid)
    {
        var sessions = new WebSessionStore(new SessionClock());
        var launch = sessions.CreateLaunchToken("revision");

        sessions.RedeemLaunchToken(invalid, "revision").Should().BeNull();

        sessions.RedeemLaunchToken(launch, "revision").Should().NotBeNull();
    }

    [Fact]
    public void Should_RevokeSessionsAndLaunches_WhenSecurityChangesOrHostRestarts()
    {
        var sessions = new WebSessionStore(new SessionClock());
        var token = sessions.Create("old");
        var launch = sessions.CreateLaunchToken("old");

        sessions.Authenticate(token, "new").Should().BeNull();

        sessions.RedeemLaunchToken(launch, "new").Should().BeNull();
        new WebSessionStore(new SessionClock()).Authenticate(token, "old").Should().BeNull();
    }

    [Fact]
    public void Should_OnlyAdvanceOwnerInitialization_WhenSetupChangesSecurity()
    {
        var sessions = new WebSessionStore(new SessionClock());
        var owner = sessions.RedeemLaunchToken(sessions.CreateLaunchToken("first-run"), "first-run")!;
        var other = sessions.Create("first-run");
        var id = sessions.Authenticate(owner, "first-run")!;
        sessions.BeginSecurityChange(id, "first-run");

        sessions.CompleteInitialization(id, "configured");

        sessions.Authenticate(owner, "configured").Should().NotBeNull();
        sessions.Authenticate(other, "configured").Should().BeNull();
        var act = () => sessions.CompleteInitialization(id, "yet-another-key");
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Should_BoundSecurityChangeWindow_WhenOperationDoesNotFinish()
    {
        var clock = new SessionClock();
        var sessions = new WebSessionStore(clock);
        var token = sessions.Create("old");
        var id = sessions.Authenticate(token, "old")!;
        sessions.BeginSecurityChange(id, "old");

        sessions.IsCurrent(id, "new").Should().BeTrue();

        clock.Advance(TimeSpan.FromMinutes(1));
        sessions.IsCurrent(id, "new").Should().BeFalse();
        sessions.EndSecurityChange(id);
        sessions.IsCurrent(id, "old").Should().BeTrue();
    }

    [Fact]
    public async Task Should_IgnoreOrdinaryPreferences_WhenComputingRevocationStamp()
    {
        var settings = new MemorySettingsStore();
        var service = new WebSecurityStamp(settings);
        var original = await service.ReadAsync(CancellationToken.None);

        await settings.SetAsync("ui.language", "ru", CancellationToken.None);

        (await service.ReadAsync(CancellationToken.None)).Revision.Should().Be(original.Revision);
        await settings.SetAsync("security.kek_check", "new-check", CancellationToken.None);
        (await service.ReadAsync(CancellationToken.None)).Revision.Should().NotBe(original.Revision);
    }
}

internal sealed class SessionClock : TimeProvider
{
    private DateTimeOffset _now = DateTimeOffset.UnixEpoch;
    public override DateTimeOffset GetUtcNow() => _now;
    public void Advance(TimeSpan amount) => _now += amount;
}
