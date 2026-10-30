using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.RateLimiting;
using MongoTui.Domain.Exceptions;
using MongoTui.Web.Components;
using MongoTui.Web.Services;

namespace MongoTui.Web.Bootstrap;

internal static class WebAuthenticationEndpoints
{
    internal const string SignInPolicy = "web-sign-in";

    public static void Map(WebApplication app)
    {
        app.MapGet("/login", () => new RazorComponentResult<LoginPage>());
        app.MapPost("/auth/password", PasswordAsync).RequireRateLimiting(SignInPolicy);
        app.MapPost("/auth/launch", LaunchAsync).RequireRateLimiting(SignInPolicy);
        app.MapGet("/auth/session", () => Results.NoContent());
        app.MapPost("/auth/logout", (HttpContext context, WebSessionStore sessions) =>
        {
            sessions.Revoke(context.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            return Results.NoContent();
        });
    }

    private static async Task<IResult> PasswordAsync(HttpContext context, WebSignInService signIn)
    {
        try
        {
            var request = await ReadAsync(context);
            if (request?.Password is not { Length: > 0 and <= 4096 } password) return Results.Unauthorized();
            var token = await signIn.PasswordAsync(password, context.RequestAborted);
            return token is null ? Results.Unauthorized() : Results.Json(new SessionResponse(token));
        }
        catch (Exception exception) when (exception is DomainException or CryptographicException)
        { return Results.Unauthorized(); }
        catch (Exception exception) when (exception is JsonException or BadHttpRequestException)
        { return Results.BadRequest(); }
        catch (Exception) when (!context.RequestAborted.IsCancellationRequested)
        { return Results.StatusCode(StatusCodes.Status503ServiceUnavailable); }
    }

    private static async Task<IResult> LaunchAsync(HttpContext context, WebSecurityStamp stamp, WebSessionStore sessions)
    {
        try
        {
            var request = await ReadAsync(context);
            var state = await stamp.ReadAsync(context.RequestAborted);
            var token = sessions.RedeemLaunchToken(request?.Launch, state.Revision);
            return token is null ? Results.Unauthorized() : Results.Json(new SessionResponse(token));
        }
        catch (Exception exception) when (exception is JsonException or BadHttpRequestException)
        { return Results.BadRequest(); }
        catch (Exception) when (!context.RequestAborted.IsCancellationRequested)
        { return Results.StatusCode(StatusCodes.Status503ServiceUnavailable); }
    }

    private static Task<SignInRequest?> ReadAsync(HttpContext context)
    {
        if (!context.Request.HasJsonContentType()) throw new BadHttpRequestException("JSON is required.");
        if (context.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } limit)
            limit.MaxRequestBodySize = 16 * 1024;
        return context.Request.ReadFromJsonAsync<SignInRequest>(context.RequestAborted).AsTask();
    }

    private sealed record SignInRequest(string? Password, string? Launch)
    {
        public override string ToString() => nameof(SignInRequest);
    }

    private sealed record SessionResponse(string Token)
    {
        public override string ToString() => nameof(SessionResponse);
    }
}
