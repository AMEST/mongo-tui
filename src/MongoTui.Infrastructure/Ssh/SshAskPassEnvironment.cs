namespace MongoTui.Infrastructure.Ssh;

/// <summary>Environment keys shared by the SSH launcher and its askpass child process.</summary>
public static class SshAskPassEnvironment
{
    public const string Executable = "SSH_ASKPASS";
    public const string Require = "SSH_ASKPASS_REQUIRE";
    public const string Force = "force";
    public const string Pipe = "MONGOTUI_ASKPASS_PIPE";
}
