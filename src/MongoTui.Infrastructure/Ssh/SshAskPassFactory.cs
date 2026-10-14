using MongoTui.Application.Abstractions.Ssh;

namespace MongoTui.Infrastructure.Ssh;

public sealed class SshAskPassFactory
{
    public SshAskPassServer Start(ITwoFactorPrompt prompt, CancellationToken cancellationToken) =>
        new(prompt, cancellationToken);
}
