namespace MongoTui.Infrastructure.Ssh;

public sealed record SshCliOptions(string AskPassExecutable)
{
    public string SshExecutable { get; init; } = "ssh";

    public TimeSpan StartupTimeout { get; init; } = TimeSpan.FromMinutes(2);
}
