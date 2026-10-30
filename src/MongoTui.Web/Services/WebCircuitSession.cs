using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace MongoTui.Web.Services;

/// <summary>Transfers a completed security change to the browser that performed it.</summary>
public sealed class WebCircuitSession(AuthenticationStateProvider authentication, WebSessionStore sessions, WebSecurityStamp stamp)
{
    public async Task BeginSecurityChangeAsync(CancellationToken cancellationToken)
    {
        var id = await IdAsync().ConfigureAwait(false);
        var state = await stamp.ReadAsync(cancellationToken).ConfigureAwait(false);
        sessions.BeginSecurityChange(id, state.Revision);
    }

    public async Task EndSecurityChangeAsync() => sessions.EndSecurityChange(await IdAsync().ConfigureAwait(false));

    public async Task CompleteInitializationAsync(CancellationToken cancellationToken)
    {
        var id = await IdAsync().ConfigureAwait(false);
        var state = await stamp.ReadAsync(cancellationToken).ConfigureAwait(false);
        sessions.CompleteInitialization(id, state.Revision);
    }

    public async Task<string> RenewAfterSecurityChangeAsync(CancellationToken cancellationToken)
    {
        var id = await IdAsync().ConfigureAwait(false);
        var state = await stamp.ReadAsync(cancellationToken).ConfigureAwait(false);
        sessions.Revoke(id);
        return sessions.Create(state.Revision);
    }

    private async Task<string> IdAsync() =>
        (await authentication.GetAuthenticationStateAsync().ConfigureAwait(false)).User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("The web session is missing.");
}
