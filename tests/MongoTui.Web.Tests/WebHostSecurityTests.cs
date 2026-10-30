using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Net.WebSockets;
using AwesomeAssertions;
using FakeItEasy;
using Microsoft.Extensions.DependencyInjection;
using MongoTui.Application.Models;
using MongoTui.Application.Services;

namespace MongoTui.Web.Tests;

public sealed class WebHostSecurityTests
{
    [Theory]
    [InlineData("/auth/session")]
    [InlineData("/_blazor/negotiate?negotiateVersion=1")]
    [InlineData("/_blazor")]
    [InlineData("/api/query")]
    public async Task Should_RejectApiAndBlazor_WhenSessionIsMissing(string path)
    {
        await using var host = await WebHostFixture.StartAsync();

        using var response = await host.Client.GetAsync(path);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Headers.WwwAuthenticate.Single().Scheme.Should().Be("Bearer");
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/login")]
    [InlineData("/_content/MongoTui.Web/app.css")]
    [InlineData("/_content/MongoTui.Web/auth.js")]
    [InlineData("/_framework/blazor.web.js")]
    [InlineData("/_blazor/initializers")]
    public async Task Should_ServePublicShellAndAssets_WhenSessionIsMissing(string path)
    {
        await using var host = await WebHostFixture.StartAsync();

        using var response = await host.Client.GetAsync(path);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.WwwAuthenticate.Should().BeEmpty();
        (await response.Content.ReadAsStringAsync()).Should().NotContain(WebHostFixture.Password);
    }

    [Fact]
    public async Task Should_IssueIndependentSession_WhenMasterPasswordIsCorrect()
    {
        await using var host = await WebHostFixture.StartAsync();

        await host.AuthenticateAsync();

        (await host.Client.GetAsync("/auth/session")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await host.Client.PostAsync("/_blazor/negotiate?negotiateVersion=1", null)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Should_AuthenticateWebSocketQueryToken_WhenBrowserCannotSetHeaders()
    {
        await using var host = await WebHostFixture.StartAsync();
        var token = await host.AuthenticateAsync();
        using var negotiated = await host.Client.PostAsync("/_blazor/negotiate?negotiateVersion=1", null);
        using var document = JsonDocument.Parse(await negotiated.Content.ReadAsStringAsync());
        var connection = document.RootElement.GetProperty("connectionToken").GetString()!;
        var uri = new UriBuilder(host.Client.BaseAddress!)
        {
            Scheme = "ws", Path = "/_blazor",
            Query = "id=" + Uri.EscapeDataString(connection) + "&access_token=" + token,
        };
        using var socket = new ClientWebSocket();
        using var lifetime = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        await socket.ConnectAsync(uri.Uri, lifetime.Token);

        socket.State.Should().Be(WebSocketState.Open);
        socket.Abort();
    }

    [Theory]
    [InlineData("en", "Master password")]
    [InlineData("ru", "Мастер-пароль")]
    public async Task Should_RenderLocalizedSinglePasswordForm_WhenSigningIn(string language, string label)
    {
        await using var host = await WebHostFixture.StartAsync();

        var page = WebUtility.HtmlDecode(await host.Client.GetStringAsync("/login?lang=" + language));

        page.Should().Contain(label).And.NotContain("Basic").And.NotContain("blazor.web.js");
    }

    [Fact]
    public async Task Should_RejectExpiredSession_WhenAnotherRequestArrives()
    {
        var clock = new SessionClock();
        await using var host = await WebHostFixture.StartAsync(clock: clock);
        await host.AuthenticateAsync();

        clock.Advance(MongoTui.Web.Services.WebSessionStore.SessionLifetime);

        (await host.Client.GetAsync("/auth/session")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Should_RejectWrongPassword_WhenProcessWasAlreadyUnlockedByAnotherBrowser()
    {
        await using var host = await WebHostFixture.StartAsync();
        var existing = await host.AuthenticateAsync();
        host.Client.DefaultRequestHeaders.Authorization = null;

        using var response = await host.Client.PostAsJsonAsync("/auth/password", new { password = "incorrect-master" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        host.Services.GetRequiredService<SecretKeyService>().IsUnlocked.Should().BeTrue();
        host.Authorize(existing);
        (await host.Client.GetAsync("/auth/session")).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Theory]
    [InlineData("https://foreign.example", "cross-site")]
    [InlineData("http://127.0.0.1:9999", "same-site")]
    [InlineData("", "same-site")]
    public async Task Should_RejectOtherOrigins_WhenTheyAttemptSignIn(string origin, string site)
    {
        await using var host = await WebHostFixture.StartAsync();
        if (origin.Length > 0) host.Client.DefaultRequestHeaders.Add("Origin", origin);
        host.Client.DefaultRequestHeaders.Add("Sec-Fetch-Site", site);

        using var response = await host.Client.PostAsJsonAsync("/auth/password", new { password = WebHostFixture.Password });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        A.CallTo(() => host.PasswordSource.UnlockAsync(A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task Should_RevokeSession_WhenAnotherProcessChangesSecurityMaterial()
    {
        await using var host = await WebHostFixture.StartAsync();
        await host.AuthenticateAsync();

        await host.Store.SetAsync("security.kek_check", "replacement-key-check", CancellationToken.None);

        (await host.Client.GetAsync("/auth/session")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await host.Client.PostAsync("/_blazor/negotiate?negotiateVersion=1", null)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Should_RevokeOnlyCurrentSession_WhenSigningOut()
    {
        await using var host = await WebHostFixture.StartAsync();
        var other = await host.AuthenticateAsync();
        var current = await host.AuthenticateAsync();

        using var response = await host.Client.PostAsJsonAsync("/auth/logout", new { });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        host.Authorize(current);
        (await host.Client.GetAsync("/auth/session")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        host.Authorize(other);
        (await host.Client.GetAsync("/auth/session")).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Should_RequireLaunchProof_WhenModeHasNoPasswordOrIsFirstRun(bool firstRun)
    {
        await using var host = await WebHostFixture.StartAsync(SecurityMode.None, firstRun);

        using var response = await host.Client.PostAsJsonAsync("/auth/password", new { password = "invented-password" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var page = await host.Client.GetStringAsync("/login");
        page.Should().NotContain("id=\"master-password\"");
        (await host.Client.PostAsJsonAsync("/auth/setup", new { mode = "none" })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Should_RedeemLaunchOnlyOnce_WhenOpeningTrustedLink()
    {
        await using var host = await WebHostFixture.StartAsync(SecurityMode.None, firstRun: true);
        var launch = await host.LaunchAsync();

        using var response = await host.Client.PostAsJsonAsync("/auth/launch", new { launch });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var token = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("token").GetString()!;
        (await host.Client.PostAsJsonAsync("/auth/launch", new { launch })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        host.Authorize(token);
        (await host.Client.PostAsync("/_blazor/negotiate?negotiateVersion=1", null)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("Basic dXNlcjpwYXNzd29yZA==")]
    [InlineData("Bearer not-a-session")]
    [InlineData("Bearer AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public async Task Should_RejectInvalidCredentials_WhenCallingApi(string header)
    {
        await using var host = await WebHostFixture.StartAsync();
        host.Client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", header);

        using var response = await host.Client.GetAsync("/auth/session");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Should_RejectTokenInOrdinaryUrl_WhenClientAttemptsToLeakOrReuseIt()
    {
        await using var host = await WebHostFixture.StartAsync();
        var token = await host.AuthenticateAsync();
        host.Client.DefaultRequestHeaders.Authorization = null;

        using var response = await host.Client.GetAsync("/auth/session?access_token=" + token);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await host.Client.GetAsync("/_blazor?access_token=" + token)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Should_LimitPasswordAttempts_WhenAnUnauthenticatedClientRetries()
    {
        await using var host = await WebHostFixture.StartAsync();
        for (var i = 0; i < 5; i++)
            (await host.Client.PostAsJsonAsync("/auth/password", new { password = "incorrect" })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        using var response = await host.Client.PostAsJsonAsync("/auth/password", new { password = WebHostFixture.Password });

        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        A.CallTo(() => host.PasswordSource.UnlockAsync(A<string>._, A<CancellationToken>._)).MustHaveHappened(5, Times.Exactly);
    }

    [Theory]
    [InlineData("{not-json", "application/json")]
    [InlineData("password=secret", "application/x-www-form-urlencoded")]
    public async Task Should_RejectMalformedSignIn_WhenBodyIsNotExpectedJson(string body, string contentType)
    {
        await using var host = await WebHostFixture.StartAsync();

        using var response = await host.Client.PostAsync("/auth/password", new StringContent(body, Encoding.UTF8, contentType));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
