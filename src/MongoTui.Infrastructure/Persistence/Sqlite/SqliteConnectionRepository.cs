using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using LinqToDB;
using LinqToDB.Async;
using MongoTui.Application.Abstractions.Persistence;
using MongoTui.Application.Models;
using MongoTui.Domain.Connections;
using MongoTui.Domain.Primitives;

namespace MongoTui.Infrastructure.Persistence.Sqlite;

/// <summary>LINQ to DB backed <see cref="IConnectionRepository"/>.</summary>
public sealed class SqliteConnectionRepository : IConnectionRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly SqliteDatabase _database;

    public SqliteConnectionRepository(SqliteDatabase database) => _database = database;

    public async Task<IReadOnlyList<StoredConnection>> GetAllAsync(CancellationToken cancellationToken)
    {
        await using var session = _database.OpenSession();
        var db = session.Connection;

        var rows = await db.Connections
            .OrderBy(connection => connection.Name)
            .ToListAsync(cancellationToken);

        return rows.Select(ToStored).ToList();
    }

    public async Task<StoredConnection?> GetAsync(ConnectionId id, CancellationToken cancellationToken)
    {
        await using var session = _database.OpenSession();
        var db = session.Connection;
        var key = id.ToString();

        var row = await db.Connections
            .FirstOrDefaultAsync(connection => connection.Id == key, cancellationToken);

        return row is null ? null : ToStored(row);
    }

    public async Task AddAsync(StoredConnection connection, CancellationToken cancellationToken)
    {
        await using var session = _database.OpenSession();
        var db = session.Connection;

        await db.InsertAsync(ToRow(connection), token: cancellationToken);
    }

    public async Task UpdateAsync(StoredConnection connection, CancellationToken cancellationToken)
    {
        await using var session = _database.OpenSession();
        var db = session.Connection;

        await db.UpdateAsync(ToRow(connection), token: cancellationToken);
    }

    public async Task DeleteAsync(ConnectionId id, CancellationToken cancellationToken)
    {
        await using var session = _database.OpenSession();
        var db = session.Connection;
        var key = id.ToString();

        await db.Connections
            .Where(connection => connection.Id == key)
            .DeleteAsync(cancellationToken);
    }

    private static ConnectionRow ToRow(StoredConnection connection)
    {
        var profile = connection.Profile;
        var options = new ConnectionOptions(profile.Auth, profile.Tls, profile.Ssh);

        return new ConnectionRow
        {
            Id = profile.Id.ToString(),
            Name = profile.Name,
            Uri = profile.Uri,
            AuthKind = profile.Auth.Kind.ToString(),
            EncryptedSecret = PackSecret(connection.Secret),
            OptionsJson = JsonSerializer.Serialize(options, JsonOptions),
            CreatedAt = FormatTimestamp(profile.CreatedAt),
            UpdatedAt = FormatTimestamp(profile.UpdatedAt),
        };
    }

    private static StoredConnection ToStored(ConnectionRow row)
    {
        var options = JsonSerializer.Deserialize<ConnectionOptions>(row.OptionsJson, JsonOptions)!;

        var profile = ConnectionProfile.Create(
            ConnectionId.Parse(row.Id),
            row.Name,
            row.Uri,
            options.Auth,
            options.Tls,
            options.Ssh,
            ParseTimestamp(row.CreatedAt),
            ParseTimestamp(row.UpdatedAt));

        return new StoredConnection(profile, UnpackSecret(row.EncryptedSecret));
    }

    private static string FormatTimestamp(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    private static DateTimeOffset ParseTimestamp(string value) =>
        DateTimeOffset.Parse(value, CultureInfo.InvariantCulture);

    private static byte[]? PackSecret(EncryptedSecret? secret)
    {
        if (secret is null)
        {
            return null;
        }

        var buffer = new byte[1 + secret.Nonce.Length + secret.CipherText.Length];
        buffer[0] = (byte)secret.Nonce.Length;
        secret.Nonce.CopyTo(buffer.AsSpan(1));
        secret.CipherText.CopyTo(buffer.AsSpan(1 + secret.Nonce.Length));

        return buffer;
    }

    private static EncryptedSecret? UnpackSecret(byte[]? blob)
    {
        if (blob is null || blob.Length == 0)
        {
            return null;
        }

        var nonceLength = blob[0];
        var nonce = blob.AsSpan(1, nonceLength).ToArray();
        var cipherText = blob.AsSpan(1 + nonceLength).ToArray();

        return new EncryptedSecret(nonce, cipherText);
    }

    private sealed record ConnectionOptions(AuthOptions Auth, TlsOptions Tls, SshTunnelOptions Ssh);
}
