namespace MongoTui.Domain.Exceptions;

/// <summary>Raised when encrypted secrets are accessed before the master key is unlocked.</summary>
public sealed class SecretsLockedException : DomainException
{
    public SecretsLockedException()
        : base("Secrets are locked; unlock them first.")
    {
    }
}
