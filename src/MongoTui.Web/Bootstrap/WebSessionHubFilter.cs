using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
using MongoTui.Web.Services;

namespace MongoTui.Web.Bootstrap;

/// <summary>Checks expiry, logout and persisted key changes on every incoming Blazor action.</summary>
public sealed class WebSessionHubFilter(WebSessionStore sessions, WebSecurityStamp stamp) : IHubFilter
{
    public async ValueTask<object?> InvokeMethodAsync(HubInvocationContext context, Func<HubInvocationContext, ValueTask<object?>> next)
    {
        await CheckAsync(context.Context);
        return await next(context);
    }

    public async Task OnConnectedAsync(HubLifetimeContext context, Func<HubLifetimeContext, Task> next)
    {
        await CheckAsync(context.Context);
        await next(context);
    }

    private async Task CheckAsync(HubCallerContext context)
    {
        var id = context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        var current = false;
        try
        {
            var state = await stamp.ReadAsync(context.ConnectionAborted);
            current = context.User?.Identity?.IsAuthenticated == true && id is not null && sessions.IsCurrent(id, state.Revision);
        }
        catch (Exception) { }
        if (current) return;
        context.Abort();
        throw new HubException("Web authentication has expired.");
    }
}
