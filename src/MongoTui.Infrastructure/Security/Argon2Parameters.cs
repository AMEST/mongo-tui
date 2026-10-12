namespace MongoTui.Infrastructure.Security;

/// <summary>Tunable Argon2id parameters for KEK derivation.</summary>
public sealed record Argon2Parameters(int MemorySizeKb, int Iterations, int DegreesOfParallelism)
{
    /// <summary>Defaults suitable for interactive use (~19 MiB, 2 passes).</summary>
    public static Argon2Parameters Default { get; } = new(19 * 1024, 2, 1);
}
