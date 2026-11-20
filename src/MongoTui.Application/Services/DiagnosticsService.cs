using System.Collections.Concurrent;
using MongoDB.Bson;
using MongoTui.Application.Abstractions.Mongo;
using MongoTui.Application.Diagnostics;
using MongoTui.Application.Models;
using MongoTui.Domain.Diagnostics;
using MongoTui.Domain.Exceptions;

namespace MongoTui.Application.Services;

public sealed class DiagnosticsService(IMongoDiagnosticsGateway gateway, TimeProvider clock)
{
    private readonly Dictionary<MongoConnectionTarget, ProfilerGlobalSettings> _globalSettings = new();
    private readonly SemaphoreSlim _changes = new(1, 1);
    private readonly ConcurrentDictionary<(MongoConnectionTarget, string), ProfilerLease> _leases = new();
    private readonly ConcurrentDictionary<(MongoConnectionTarget, string), ProfilerState> _states = new();
    public IReadOnlyList<ProfilerState> States(MongoConnectionTarget target) =>
        _states.Where(pair => pair.Key.Item1 == target).Select(pair => pair.Value).ToArray();
    public IReadOnlyList<ProfilerLease> Leases => _leases.Values.ToArray();
    public async Task RefreshKnownProfilerStatesAsync(CancellationToken cancellationToken)
    {
        foreach (var (target, database) in _states.Keys)
            await RefreshProfilerStateAsync(target, database, cancellationToken).ConfigureAwait(false);
    }

    public async Task RefreshProfilerStateAsync(MongoConnectionTarget target, string database, CancellationToken cancellationToken)
    {
        try { await GetProfilerStateAsync(target, database, cancellationToken).ConfigureAwait(false); }
        catch (OperationCanceledException) { throw; }
        catch (Exception exception)
        {
            if (_states.TryGetValue((target, database), out var previous))
                _states[(target, database)] = previous with { IsAvailable = false,
                    ErrorCode = exception is DiagnosticsException diagnostic ? diagnostic.Key : "Diagnostics.Error.Command" };
        }
    }

    public async Task<ProfilerState> GetProfilerStateAsync(MongoConnectionTarget target, string database, CancellationToken cancellationToken)
    {
        var state = await gateway.GetProfilerStateAsync(target, database, cancellationToken).ConfigureAwait(false);
        _states[(target, database)] = state;
        return state;
    }

    public async Task<IReadOnlyList<ProfilerApplyResult>> ApplyProfilerAsync(MongoConnectionTarget target,
        ProfilerApplyRequest request, CancellationToken cancellationToken)
    {
        Validate(request);
        await _changes.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var results = new List<ProfilerApplyResult>();
            foreach (var database in request.Databases.Distinct(StringComparer.Ordinal))
            {
                cancellationToken.ThrowIfCancellationRequested();
                try { results.Add(await ApplyOneAsync(target, database, request, cancellationToken).ConfigureAwait(false)); }
                catch (OperationCanceledException) { throw; }
                catch (Exception exception) { results.Add(new(database, request.Level, null,
                    exception is DiagnosticsException diagnostic ? diagnostic.Key : "Diagnostics.Error.Command")); }
            }
            return results;
        }
        finally { _changes.Release(); }
    }

    private async Task<ProfilerApplyResult> ApplyOneAsync(MongoConnectionTarget target, string database,
        ProfilerApplyRequest request, CancellationToken cancellationToken)
    {
        var before = await GetProfilerStateAsync(target, database, cancellationToken).ConfigureAwait(false);
        var key = (target, database);
        var globals = GetGlobalSettings(target, before.Options);
        var original = _leases.TryGetValue(key, out var previous)
            && (previous.RestoreOnExit || previous.ExpiresAt is not null)
            && MatchesDatabaseState(before, previous.Applied) ? previous.Original : before;
        if (request.Level != ProfilingLevel.Off && previous is null)
        {
            var tentative = before with { Level = request.Level, Options = request.Options with
                { FilterJson = request.FilterAction == ProfilerFilterAction.Preserve ? before.Options.FilterJson
                    : request.FilterAction == ProfilerFilterAction.Clear ? null : BsonTextCodec.ParseDocument(request.Options.FilterJson!).ToJson() } };
            _leases[key] = new ProfilerLease(target, database, original, tentative, request.RestoreOnExit,
                request.AutoDisableMinutes > 0 ? clock.GetUtcNow().AddMinutes(request.AutoDisableMinutes) : null, request.AutoDisableMinutes);
        }
        var actual = await gateway.SetProfilerLevelAsync(target, database, request.Level, request.Options,
            request.FilterAction, cancellationToken).ConfigureAwait(false);
        _states[key] = actual;
        if (actual.Level == ProfilingLevel.Off)
        {
            globals.Keep(database);
            _leases.TryRemove(key, out _);
            ForgetGlobalSettingsIfUnused(target);
        }
        else
        {
            var expiresAt = request.AutoDisableMinutes > 0 ? clock.GetUtcNow().AddMinutes(request.AutoDisableMinutes) : (DateTimeOffset?)null;
            _leases[key] = new ProfilerLease(target, database, original, actual, request.RestoreOnExit, expiresAt, request.AutoDisableMinutes);
            globals.Apply(request.RestoreOnExit || expiresAt is not null ? database : null, actual.Options);
        }
        return new(database, request.Level, actual, null);
    }

    public async Task RestoreAsync(ProfilerLease lease, CancellationToken cancellationToken)
    {
        await _changes.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { await RestoreCoreAsync(lease, cancellationToken).ConfigureAwait(false); }
        finally { _changes.Release(); }
    }

    private async Task RestoreCoreAsync(ProfilerLease lease, CancellationToken cancellationToken)
    {
        var key = (lease.Target, lease.Database);
        if (!_leases.TryGetValue(key, out var current) || current != lease) return;
        var actual = await GetProfilerStateAsync(lease.Target, lease.Database, cancellationToken).ConfigureAwait(false);
        var globals = GetGlobalSettings(lease.Target, actual.Options);
        if (MatchesDatabaseState(actual, lease.Applied))
        {
            var globalOptions = globals.RestorationFor(lease.Database);
            await gateway.RestoreProfilerAsync(lease.Target, lease.Original, globalOptions, cancellationToken).ConfigureAwait(false);
            globals.Remove(lease.Database);
            await GetProfilerStateAsync(lease.Target, lease.Database, cancellationToken).ConfigureAwait(false);
        }
        else globals.Keep(lease.Database);
        _leases.TryRemove(key, out _);
        ForgetGlobalSettingsIfUnused(lease.Target);
    }

    public async Task DisconnectAsync(MongoConnectionTarget target, CancellationToken cancellationToken)
    {
        await _changes.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { await DisconnectCoreAsync(target, cancellationToken).ConfigureAwait(false); }
        finally { _changes.Release(); }
    }

    private async Task DisconnectCoreAsync(MongoConnectionTarget target, CancellationToken cancellationToken)
    {
        var failed = false;
        var leases = Leases.Where(item => item.Target == target).ToArray();
        foreach (var lease in leases.Where(item => !item.RestoreOnExit))
        {
            if (_globalSettings.TryGetValue(target, out var globals)) globals.Keep(lease.Database);
            _leases.TryRemove((target, lease.Database), out _);
        }
        foreach (var lease in leases.Where(item => item.RestoreOnExit))
        {
            try { await RestoreCoreAsync(lease, cancellationToken).ConfigureAwait(false); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception)
            {
                if (_globalSettings.TryGetValue(target, out var globals)) globals.Keep(lease.Database);
                _leases.TryRemove((target, lease.Database), out _);
                failed = true;
            }
        }
        foreach (var key in _states.Keys.Where(key => key.Item1 == target)) _states.TryRemove(key, out _);
        ForgetGlobalSettingsIfUnused(target);
        if (failed) throw new DiagnosticsException("Diagnostics.Error.Command");
    }

    public Task<ServerOverview> GetServerOverviewAsync(MongoConnectionTarget target, CancellationToken cancellationToken) =>
        gateway.GetServerStatusAsync(target, cancellationToken);
    public Task<DatabaseOverview> GetDatabaseOverviewAsync(MongoConnectionTarget target, string database, CancellationToken cancellationToken) =>
        gateway.GetDatabaseOverviewAsync(target, database, cancellationToken);
    public Task<IReadOnlyList<ProfilerEntry>> ReadProfilerAsync(MongoConnectionTarget target, string database,
        ProfilerReadFilter filter, int limit, CancellationToken cancellationToken) =>
        gateway.ReadProfilerAsync(target, database, filter, limit, cancellationToken);

    private ProfilerGlobalSettings GetGlobalSettings(MongoConnectionTarget target, ProfilerOptions actual)
    {
        if (!_globalSettings.TryGetValue(target, out var globals))
            _globalSettings[target] = globals = new ProfilerGlobalSettings(actual);
        else globals.Observe(actual);
        return globals;
    }

    private void ForgetGlobalSettingsIfUnused(MongoConnectionTarget target)
    {
        if (!_leases.Keys.Any(key => key.Item1 == target)) _globalSettings.Remove(target);
    }

    private static bool MatchesDatabaseState(ProfilerState actual, ProfilerState applied) =>
        actual.Level == applied.Level && actual.Options.FilterJson == applied.Options.FilterJson;

    private static void Validate(ProfilerApplyRequest request)
    {
        if (!Enum.IsDefined(request.Level) || !Enum.IsDefined(request.FilterAction)) throw new ArgumentOutOfRangeException(nameof(request));
        if (request.Level == ProfilingLevel.All && !request.ConfirmAll) throw new DiagnosticsException("Diagnostics.ProfilerWarningAll");
        if (request.Databases.Count == 0) throw new ArgumentException("Select at least one database.");
        foreach (var database in request.Databases) MongoTui.Domain.Primitives.DatabaseName.Create(database);
        ArgumentOutOfRangeException.ThrowIfNegative(request.Options.SlowMs);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(request.Options.SlowMs, 600000);
        if (!double.IsFinite(request.Options.SampleRate) || request.Options.SampleRate is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(request));
        if (request.AutoDisableMinutes is < 0 or > 1440) throw new ArgumentOutOfRangeException(nameof(request));
        if (request.FilterAction == ProfilerFilterAction.Replace) BsonTextCodec.ParseDocument(request.Options.FilterJson ?? "");
    }
}

public sealed record ProfilerLease(MongoConnectionTarget Target, string Database, ProfilerState Original,
    ProfilerState Applied, bool RestoreOnExit, DateTimeOffset? ExpiresAt, int AutoDisableMinutes);
