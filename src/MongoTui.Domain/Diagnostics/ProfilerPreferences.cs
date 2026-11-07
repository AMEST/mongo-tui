namespace MongoTui.Domain.Diagnostics;

/// <summary>Form defaults only. Loading them must never change server profiling.</summary>
public sealed record ProfilerPreferences
{
    public SlowQueryMode Mode { get; init; }
    public IReadOnlyList<string> Databases { get; init; } = [];
    public int SlowMs { get; init; } = 100;
    public double SampleRate { get; init; } = 1;
    public bool RestoreOnExit { get; init; }
    public int AutoDisableMinutes { get; init; }

    public static ProfilerPreferences Defaults(SlowQueryMode mode) => new()
    {
        Mode = mode, RestoreOnExit = mode == SlowQueryMode.ServerAll,
        AutoDisableMinutes = mode == SlowQueryMode.ServerAll ? 15 : 0,
    };
}
