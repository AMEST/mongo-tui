using AwesomeAssertions;
using Microsoft.AspNetCore.Http;
using MongoTui.Web.Bootstrap;

namespace MongoTui.Web.Tests;

public sealed class LocalRequestGuardTests
{
    [Theory]
    [InlineData("127.0.0.1:8765", "", "", true)]
    [InlineData("localhost:8765", "http://localhost:8765", "same-origin", true)]
    [InlineData("attacker.example:8765", "", "", false)]
    [InlineData("127.0.0.1:80", "", "", false)]
    [InlineData("127.0.0.1:8765", "https://attacker.example", "", false)]
    [InlineData("127.0.0.1:8765", "null", "", false)]
    [InlineData("127.0.0.1:8765", "", "cross-site", false)]
    public async Task Should_EnforceLocalOrigin_WhenRequestArrives(string host, string origin, string site, bool allowed)
    {
        var context = new DefaultHttpContext();
        context.Connection.LocalPort = 8765;
        context.Request.Host = new HostString(host);
        context.Request.Scheme = "http";
        context.Request.Headers.Origin = origin;
        context.Request.Headers["Sec-Fetch-Site"] = site;
        var invoked = false;
        var guard = new LocalRequestGuard(_ => { invoked = true; return Task.CompletedTask; });

        await guard.InvokeAsync(context);

        invoked.Should().Be(allowed);
        context.Response.StatusCode.Should().Be(allowed ? 200 : 403);
        if (allowed) context.Response.Headers.XFrameOptions.ToString().Should().Be("DENY");
    }
}
