using System.Text;
using MongoTui.Domain.Exceptions;

namespace MongoTui.Domain.Primitives;

/// <summary>Validated MongoDB database name.</summary>
public sealed record DatabaseName
{
    private const int MaxBytes = 64;

    private static readonly char[] ForbiddenCharacters =
        ['/', '\\', '.', ' ', '"', '$', '*', '<', '>', ':', '|', '?'];

    private DatabaseName(string value) => Value = value;

    public string Value { get; }

    public static DatabaseName Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException("Database name must not be empty.");
        }

        if (value.IndexOfAny(ForbiddenCharacters) >= 0)
        {
            throw new DomainException($"Database name '{value}' contains forbidden characters.");
        }

        if (Encoding.UTF8.GetByteCount(value) > MaxBytes)
        {
            throw new DomainException($"Database name '{value}' exceeds {MaxBytes} bytes.");
        }

        return new DatabaseName(value);
    }

    public override string ToString() => Value;
}
