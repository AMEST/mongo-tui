using MongoTui.Domain.Exceptions;

namespace MongoTui.Application.Models;

/// <summary>An expected editing failure with a resource key shared by both interfaces.</summary>
public sealed class DocumentEditException : DomainException
{
    public DocumentEditException(string message, string errorKey) : base(message) => ErrorKey = errorKey;

    public DocumentEditException(string message, string errorKey, Exception innerException) : base(message, innerException) => ErrorKey = errorKey;

    public string ErrorKey { get; }
}
