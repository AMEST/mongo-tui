using MongoTui.Application.Abstractions.Security;

namespace MongoTui.Application.Services;

internal static class KekSourceCleanup
{
    internal static async Task DeleteAsync(IKekSource source, CancellationToken cancellationToken)
    {
        try
        {
            await source.DeleteAsync(cancellationToken);
        }
        catch (Exception)
        {
        }
    }
}
