using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace MongoTui.Web.Services;

/// <summary>Opaque, revocable, process-local sessions. Only hashes of bearer and launch tokens are retained.</summary>
public sealed class WebSessionStore(TimeProvider clock)
{
    public static readonly TimeSpan SessionLifetime = TimeSpan.FromHours(8);
    public static readonly TimeSpan LaunchLifetime = TimeSpan.FromMinutes(15);
    private const int MaximumSessions = 128;
    private readonly ConcurrentDictionary<string, Session> _sessions = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, Session> _launches = new(StringComparer.Ordinal);
    private readonly object _gate = new();

    public string Create(string revision, bool canInitialize = false)
    {
        lock (_gate)
        {
            RemoveExpired(_sessions);
            if (_sessions.Count >= MaximumSessions) throw new InvalidOperationException("Too many web sessions.");
            var token = Token();
            _sessions[Hash(token)!] = new Session(revision, clock.GetUtcNow() + SessionLifetime, canInitialize);
            return token;
        }
    }

    public string CreateLaunchToken(string revision)
    {
        lock (_gate)
        {
            RemoveExpired(_launches);
            _launches.Clear();
            var token = Token();
            _launches[Hash(token)!] = new Session(revision, clock.GetUtcNow() + LaunchLifetime, true);
            return token;
        }
    }

    public string? RedeemLaunchToken(string? token, string revision)
    {
        var hash = Hash(token);
        return hash is not null && _launches.TryRemove(hash, out var launch)
            && Valid(launch, revision) ? Create(revision, canInitialize: true) : null;
    }

    public string? Authenticate(string? token, string revision)
    {
        var id = Hash(token);
        return id is not null && IsCurrent(id, revision) ? id : null;
    }

    public bool IsCurrent(string id, string revision) =>
        _sessions.TryGetValue(id, out var session) && Valid(session, revision);

    public void BeginSecurityChange(string id, string revision)
    {
        lock (_gate)
        {
            if (!IsCurrent(id, revision)) throw new InvalidOperationException("The web session has expired.");
            _sessions[id] = _sessions[id] with { UpdatingUntil = clock.GetUtcNow() + TimeSpan.FromMinutes(1) };
        }
    }

    public void EndSecurityChange(string id)
    {
        lock (_gate)
        {
            if (_sessions.TryGetValue(id, out var session)) _sessions[id] = session with { UpdatingUntil = null };
        }
    }

    /// <summary>Only launch-authorized sessions can adopt the initial setup or legacy migration.</summary>
    public void CompleteInitialization(string id, string revision)
    {
        lock (_gate)
        {
            if (!_sessions.TryGetValue(id, out var session) || session.ExpiresAt <= clock.GetUtcNow())
                throw new InvalidOperationException("Web initialization is not authorized.");
            if (!session.CanInitialize && session.Revision != revision)
                throw new InvalidOperationException("Web initialization is not authorized.");
            _sessions[id] = session with { Revision = revision, CanInitialize = false, UpdatingUntil = null };
        }
    }

    public void Revoke(string id) => _sessions.TryRemove(id, out _);

    private bool Valid(Session session, string revision) => session.ExpiresAt > clock.GetUtcNow()
        && (session.Revision == revision || session.UpdatingUntil > clock.GetUtcNow());
    private void RemoveExpired(ConcurrentDictionary<string, Session> sessions)
    {
        foreach (var pair in sessions)
            if (pair.Value.ExpiresAt <= clock.GetUtcNow()) sessions.TryRemove(pair.Key, out _);
    }

    private static string Token() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    private static string? Hash(string? token) => token is { Length: 64 } && token.All(char.IsAsciiHexDigit)
        ? Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(token))) : null;
    private sealed record Session(string Revision, DateTimeOffset ExpiresAt, bool CanInitialize, DateTimeOffset? UpdatingUntil = null);
}
