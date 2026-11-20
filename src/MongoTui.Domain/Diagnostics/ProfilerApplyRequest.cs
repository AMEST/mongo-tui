namespace MongoTui.Domain.Diagnostics;

public enum ProfilerFilterAction { Preserve, Replace, Clear }
public sealed record ProfilerApplyRequest(IReadOnlyList<string> Databases, ProfilingLevel Level,
    ProfilerOptions Options, bool ConfirmAll = false, bool RestoreOnExit = true,
    int AutoDisableMinutes = 15, ProfilerFilterAction FilterAction = ProfilerFilterAction.Preserve);
public sealed record ProfilerApplyResult(string Database, ProfilingLevel Requested, ProfilerState? Actual, string? Error);
