using MongoTui.Application.Services;
using MongoTui.Domain.Diagnostics;

namespace MongoTui.Application.Diagnostics;

public sealed record ProfilerFormState(SlowQueryMode Mode, ProfilerOptions Options, bool RestoreOnExit, int AutoDisableMinutes)
{
    public static ProfilerFormState From(ProfilerState actual, ClientDiagnosticsSettings client,
        ProfilerPreferences? preferences, ProfilerLease? lease)
    {
        var mode = actual.Level switch
        {
            ProfilingLevel.All => SlowQueryMode.ServerAll,
            ProfilingLevel.Slow => SlowQueryMode.ServerSlow,
            _ => client.LogSlowQueries ? SlowQueryMode.Client : SlowQueryMode.Off,
        };
        var policy = preferences?.Mode == mode ? preferences : ProfilerPreferences.Defaults(mode);
        var options = mode == SlowQueryMode.Client ? actual.Options with { SlowMs = client.SlowThresholdMs } : actual.Options;
        return new(mode, options, lease?.RestoreOnExit ?? policy.RestoreOnExit, lease?.AutoDisableMinutes ?? policy.AutoDisableMinutes);
    }

    public static bool IsMixed(IReadOnlyList<ProfilerState> states) => states.Count > 1
        && states.Skip(1).Any(state => state.Level != states[0].Level || state.Options != states[0].Options);
}
