namespace MongoTui.Domain.Diagnostics;

public sealed record ProfilerOptions
{
    public int SlowMs { get; init; } = 100;
    public double SampleRate { get; init; } = 1.0;
    public string? FilterJson { get; init; }
}

