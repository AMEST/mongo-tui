using MongoTui.Domain.Exceptions;

namespace MongoTui.Domain.Connections;

/// <summary>Settings for an automatically started OpenSSH SOCKS5 proxy.</summary>
public sealed record SshTunnelOptions
{
    public bool Enabled { get; init; }

    public string? Host { get; init; }

    public int Port { get; init; } = 22;

    public string? Username { get; init; }

    public string? PrivateKeyFile { get; init; }

    /// <summary>Local loopback port; zero selects an available port.</summary>
    public int LocalPort { get; init; }

    public void Validate()
    {
        if (!Enabled)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(Host) || Host.StartsWith('-')
            || Host.Any(char.IsWhiteSpace) || Host.Any(char.IsControl) || Host.Contains('@'))
        {
            throw new DomainException("SSH host must be a hostname or an SSH config alias.");
        }

        if (Port is < 1 or > 65535 || LocalPort is < 0 or > 65535)
        {
            throw new DomainException("SSH port must be 1–65535; local SOCKS port must be 0–65535.");
        }

        if (Username?.Any(char.IsControl) == true)
        {
            throw new DomainException("SSH username must not contain control characters.");
        }
    }
}
