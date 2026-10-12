namespace MongoTui.Domain.Exceptions;

/// <summary>Raised when a connection to a MongoDB server cannot be established.</summary>
public sealed class ConnectionFailedException : DomainException
{
    public ConnectionFailedException(string message)
        : base(message)
    {
    }

    public ConnectionFailedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
