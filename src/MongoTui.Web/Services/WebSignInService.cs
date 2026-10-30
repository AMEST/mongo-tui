using MongoTui.Application.Models;
using MongoTui.Application.Services;

namespace MongoTui.Web.Services;

/// <summary>Proves the password anew for each browser, regardless of the process-wide unlocked key.</summary>
public sealed class WebSignInService(SecurityModeService security, WebSecurityStamp stamp, WebSessionStore sessions)
{
    public async Task<string?> PasswordAsync(string password, CancellationToken cancellationToken)
    {
        var before = await stamp.ReadAsync(cancellationToken).ConfigureAwait(false);
        if (!before.PasswordEnabled) return null;
        var detection = await security.DetectAsync(cancellationToken).ConfigureAwait(false);
        if (detection is not { State: SecurityState.Ready, Mode: SecurityMode.Password }) return null;
        await security.UnlockWithPasswordAsync(password, cancellationToken).ConfigureAwait(false);
        var after = await stamp.ReadAsync(cancellationToken).ConfigureAwait(false);
        return before.Revision == after.Revision ? sessions.Create(after.Revision) : null;
    }
}
