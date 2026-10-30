using System.Security.Claims;
using AwesomeAssertions;
using FakeItEasy;
using Microsoft.AspNetCore.SignalR;
using MongoTui.Testing;
using MongoTui.Web.Bootstrap;
using MongoTui.Web.Services;

namespace MongoTui.Web.Tests;

public sealed class WebSessionHubFilterTests
{
    [Theory]
    [InlineData("valid")]
    [InlineData("revoked")]
    [InlineData("expired")]
    [InlineData("changed")]
    public async Task Should_RevalidateOpenConnection_WhenSessionChanges(string change)
    {
        var clock = new SessionClock();
        var store = new MemorySettingsStore();
        var stamp = new WebSecurityStamp(store);
        var sessions = new WebSessionStore(clock);
        var revision = (await stamp.ReadAsync(CancellationToken.None)).Revision;
        var id = sessions.Authenticate(sessions.Create(revision), revision)!;
        var caller = A.Fake<HubCallerContext>();
        A.CallTo(() => caller.User).Returns(new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, id)], "WebSession")));
        var provider = A.Fake<IServiceProvider>();
        var hub = new ProbeHub();
        var filter = new WebSessionHubFilter(sessions, stamp);
        await filter.OnConnectedAsync(new HubLifetimeContext(caller, provider, hub), _ => Task.CompletedTask);
        if (change == "revoked") sessions.Revoke(id);
        if (change == "expired") clock.Advance(WebSessionStore.SessionLifetime);
        if (change == "changed") await store.SetAsync("security.kek_salt", "changed", CancellationToken.None);
        var context = new HubInvocationContext(caller, provider, hub, typeof(ProbeHub).GetMethod(nameof(ProbeHub.Query))!, []);
        var invoked = false;

        var error = await Record.ExceptionAsync(async () => await filter.InvokeMethodAsync(context,
            _ => { invoked = true; return ValueTask.FromResult<object?>("query executed"); }));

        invoked.Should().Be(change == "valid");
        if (change != "valid")
        {
            error.Should().BeOfType<HubException>();
            A.CallTo(() => caller.Abort()).MustHaveHappenedOnceExactly();
        }
        else error.Should().BeNull();
    }

    private sealed class ProbeHub : Hub
    {
        public void Query() { }
    }
}
