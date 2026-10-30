using System.Text;
using MongoTui.Domain.Exceptions;

namespace MongoTui.Domain.Primitives;

/// <summary>Validated MongoDB collection name.</summary>
public sealed record CollectionName
{
    private const int MaxBytes = 255;

    private CollectionName(string value) => Value = value;

    public string Value { get; }

    /// <summary>Validates an identifier, including system collection names returned by MongoDB.</summary>
    public static CollectionName Create(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            throw new DomainException("Collection name must not be empty.");
        }

        if (value.Contains('$'))
        {
            throw new DomainException("Collection name must not contain '$'.");
        }

        if (value.Contains('\0'))
        {
            throw new DomainException("Collection name must not contain the null character.");
        }

        if (Encoding.UTF8.GetByteCount(value) > MaxBytes)
        {
            throw new DomainException($"Collection name '{value}' exceeds {MaxBytes} bytes.");
        }

        return new CollectionName(value);
    }

    /// <summary>Rejects the reserved system prefix when explicitly creating a new collection.</summary>
    public void ValidateForCreation()
    {
        if (Value.StartsWith("system.", StringComparison.Ordinal))
        {
            throw new DomainException("Collection name must not start with 'system.'.");
        }
    }

    public override string ToString() => Value;
}
