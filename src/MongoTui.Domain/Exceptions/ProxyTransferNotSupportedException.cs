namespace MongoTui.Domain.Exceptions;

/// <summary>Raised when a bulk transfer cannot honor the connection's SOCKS proxy.</summary>
public sealed class ProxyTransferNotSupportedException : DomainException
{
    public ProxyTransferNotSupportedException()
        : base("MongoDB Database Tools do not support SOCKS proxies. The operation was blocked to prevent a direct connection to MongoDB.")
    {
    }
}
