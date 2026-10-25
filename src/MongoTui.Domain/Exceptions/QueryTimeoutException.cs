namespace MongoTui.Domain.Exceptions;

/// <summary>Distinguishes the configured query deadline from user cancellation.</summary>
public sealed class QueryTimeoutException(TimeSpan timeout, Exception innerException)
    : DomainException($"Query timed out after {timeout.TotalSeconds} seconds.", innerException)
{
    public TimeSpan Timeout { get; } = timeout;
}
