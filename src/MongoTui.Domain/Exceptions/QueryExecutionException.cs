namespace MongoTui.Domain.Exceptions;

/// <summary>Raised when a query fails to execute against MongoDB.</summary>
public sealed class QueryExecutionException : DomainException
{
    public QueryExecutionException(string message)
        : base(message)
    {
    }

    public QueryExecutionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
