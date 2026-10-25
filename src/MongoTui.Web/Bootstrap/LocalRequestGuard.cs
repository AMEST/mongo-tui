using Microsoft.AspNetCore.Http;

namespace MongoTui.Web.Bootstrap;

/// <summary>Rejects DNS rebinding and cross-origin requests, including WebSocket upgrades.</summary>
public sealed class LocalRequestGuard(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (!IsAllowed(context.Request))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }
        context.Response.Headers.XContentTypeOptions = "nosniff";
        context.Response.Headers.XFrameOptions = "DENY";
        context.Response.Headers["Referrer-Policy"] = "no-referrer";
        context.Response.Headers["Content-Security-Policy"] = "frame-ancestors 'none'; object-src 'none'; base-uri 'self'";
        await next(context);
    }

    internal static bool IsAllowed(HttpRequest request)
    {
        if (request.Host.Host is not ("127.0.0.1" or "localhost")
            || (request.Host.Port ?? (request.Scheme == "https" ? 443 : 80)) != request.HttpContext.Connection.LocalPort)
            return false;
        var origin = request.Headers.Origin.ToString();
        if (origin.Length > 0 && !origin.Equals($"{request.Scheme}://{request.Host}", StringComparison.Ordinal))
            return false;
        return request.Headers["Sec-Fetch-Site"] != "cross-site";
    }
}
