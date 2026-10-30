using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using MongoTui.Web.Services;

namespace MongoTui.Web.Bootstrap;

/// <summary>Protects every API and Blazor transport; the document shell and embedded assets are public.</summary>
public sealed class WebSessionAuthenticationMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, WebSessionStore sessions, WebSecurityStamp stamp)
    {
        context.Response.Headers.CacheControl = "no-store";
        if (Public(context.Request)) { await next(context); return; }
        string? id;
        try
        {
            var state = await stamp.ReadAsync(context.RequestAborted);
            id = sessions.Authenticate(ReadToken(context.Request), state.Revision);
        }
        catch (Exception) when (!context.RequestAborted.IsCancellationRequested)
        { context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable; return; }
        if (id is null)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.Headers.WWWAuthenticate = "Bearer";
            return;
        }
        context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, id)], "WebSession"));
        await next(context);
    }

    private static bool Public(HttpRequest request) =>
        ((HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method))
            && (request.Path is { Value: "/" or "/login" }
                || request.Path.StartsWithSegments("/_content/MongoTui.Web")
                || request.Path == "/_framework/blazor.web.js" || request.Path == "/_blazor/initializers"))
        || (HttpMethods.IsPost(request.Method) && request.Path is { Value: "/auth/password" or "/auth/launch" });

    private static string? ReadToken(HttpRequest request)
    {
        var header = request.Headers.Authorization;
        if (header.Count == 1 && header[0] is { } value && value.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return value[7..];
        if (header.Count != 0 || request.Path != "/_blazor"
            || (!request.HttpContext.WebSockets.IsWebSocketRequest && !request.Headers.Accept.ToString().StartsWith("text/event-stream", StringComparison.Ordinal)))
            return null;
        var query = request.Query["access_token"];
        return query.Count == 1 ? query[0] : null;
    }
}
