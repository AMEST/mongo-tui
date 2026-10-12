using MongoTui.Domain.Exceptions;

namespace MongoTui.Domain.Primitives;

/// <summary>Strongly-typed identifier of a saved connection profile.</summary>
public sealed record ConnectionId
{
    private ConnectionId(Guid value) => Value = value;

    public Guid Value { get; }

    public static ConnectionId New() => new(Guid.NewGuid());

    public static ConnectionId From(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new DomainException("Connection id must not be empty.");
        }

        return new ConnectionId(value);
    }

    public static ConnectionId Parse(string value)
    {
        if (!Guid.TryParse(value, out var guid))
        {
            throw new DomainException($"'{value}' is not a valid connection id.");
        }

        return From(guid);
    }

    public override string ToString() => Value.ToString("D");
}
