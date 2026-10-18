using MongoTui.Application.Models;
using MongoTui.Application.Services;
using MongoTui.Domain.Connections;
using MongoTui.Domain.Exceptions;
using MongoTui.Domain.Primitives;

namespace MongoTui.TUI.ViewModels;

/// <summary>Backs the create/edit connection dialog.</summary>
public sealed class ConnectionFormViewModel : ObservableObject
{
    private readonly ConnectionService _connections;

    private string _name = string.Empty;
    private string _uri = "mongodb://localhost:27017";
    private AuthKind _authKind = AuthKind.None;
    private string? _username;
    private string? _password;
    private string? _authDatabase;
    private string? _validationError;
    private SshTunnelOptions _ssh = new();
    private TlsOptions _tls = new();

    public ConnectionFormViewModel(ConnectionService connections) => _connections = connections;

    public ConnectionId Id { get; private set; } = ConnectionId.New();

    public bool IsNew { get; private set; } = true;

    public SshTunnelOptions Ssh
    {
        get => _ssh;
        set => SetProperty(ref _ssh, value);
    }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string Uri
    {
        get => _uri;
        set => SetProperty(ref _uri, value);
    }

    public AuthKind AuthKind
    {
        get => _authKind;
        set => SetProperty(ref _authKind, value);
    }

    public string? Username
    {
        get => _username;
        set => SetProperty(ref _username, value);
    }

    public string? Password
    {
        get => _password;
        set => SetProperty(ref _password, value);
    }

    public string? AuthDatabase
    {
        get => _authDatabase;
        set => SetProperty(ref _authDatabase, value);
    }

    public string? ValidationError
    {
        get => _validationError;
        private set => SetProperty(ref _validationError, value);
    }

    public void LoadNew()
    {
        Id = ConnectionId.New();
        IsNew = true;
        Name = string.Empty;
        Uri = "mongodb://localhost:27017";
        AuthKind = AuthKind.None;
        Username = null;
        Password = null;
        AuthDatabase = null;
        ValidationError = null;
        Ssh = new SshTunnelOptions();
        _tls = new TlsOptions();
        OnPropertyChanged(nameof(Id));
        OnPropertyChanged(nameof(IsNew));
    }

    public void Load(ConnectionProfile profile, string? secret)
    {
        Id = profile.Id;
        IsNew = false;
        Name = profile.Name;
        Uri = profile.Uri;
        AuthKind = profile.Auth.Kind;
        Username = profile.Auth.Username;
        AuthDatabase = profile.Auth.AuthDatabase;
        Password = secret;
        Ssh = profile.Ssh;
        _tls = profile.Tls;
        ValidationError = null;
        OnPropertyChanged(nameof(Id));
        OnPropertyChanged(nameof(IsNew));
    }

    public async Task<bool> SaveAsync(CancellationToken cancellationToken)
    {
        ValidationError = null;
        MergeCredentialsFromUri();

        try
        {
            var profile = BuildProfile();
            await _connections.SaveAsync(profile, Password, cancellationToken);
            IsNew = false;
            OnPropertyChanged(nameof(IsNew));

            return true;
        }
        catch (DomainException exception)
        {
            ValidationError = exception.Message;
            return false;
        }
    }

    public async Task<string?> TestAsync(CancellationToken cancellationToken)
    {
        ValidationError = null;
        MergeCredentialsFromUri();

        try
        {
            var profile = BuildProfile();
            await _connections.TestConnectionAsync(profile, Password, cancellationToken);

            return null;
        }
        catch (DomainException exception)
        {
            ValidationError = exception.Message;
            return exception.Message;
        }
    }

    /// <summary>
    /// Moves credentials embedded in the URI into the login/password fields when both are
    /// empty, so a pasted connection string is saved without pressing "From URI". The URI
    /// itself is stored without userinfo, keeping plaintext credentials out of the profile.
    /// </summary>
    internal void MergeCredentialsFromUri()
    {
        if (!string.IsNullOrEmpty(Username) || !string.IsNullOrEmpty(Password))
        {
            return;
        }

        ParsedConnectionUri parsed;
        try
        {
            parsed = ConnectionUriParser.Parse(Uri);
        }
        catch (DomainException exception)
        {
            ValidationError = exception.Message;
            return;
        }

        if (string.IsNullOrEmpty(parsed.Username) && string.IsNullOrEmpty(parsed.Password))
        {
            return;
        }

        Uri = parsed.Uri;

        if (!string.IsNullOrEmpty(parsed.Username))
        {
            Username = parsed.Username;
        }

        if (!string.IsNullOrEmpty(parsed.Password))
        {
            Password = parsed.Password;
        }

        if (string.IsNullOrEmpty(AuthDatabase) && !string.IsNullOrEmpty(parsed.AuthDatabase))
        {
            AuthDatabase = parsed.AuthDatabase;
        }

        if (AuthKind == AuthKind.None && parsed.AuthKind != AuthKind.None)
        {
            AuthKind = parsed.AuthKind;
        }
    }

    /// <summary>
    /// Parses a full connection string (with credentials) and fills the form fields.
    /// Credentials are moved out of the URI into the username/password fields.
    /// </summary>
    public bool ApplyUri(string connectionString)
    {
        ValidationError = null;

        try
        {
            var parsed = ConnectionUriParser.Parse(connectionString);

            Uri = parsed.Uri;
            if (!string.IsNullOrEmpty(parsed.Username))
            {
                Username = parsed.Username;
            }

            if (!string.IsNullOrEmpty(parsed.Password))
            {
                Password = parsed.Password;
            }

            if (!string.IsNullOrEmpty(parsed.AuthDatabase))
            {
                AuthDatabase = parsed.AuthDatabase;
            }

            if (parsed.AuthKind != AuthKind.None)
            {
                AuthKind = parsed.AuthKind;
            }

            if (string.IsNullOrWhiteSpace(Name))
            {
                Name = parsed.Host;
            }

            return true;
        }
        catch (DomainException exception)
        {
            ValidationError = exception.Message;
            return false;
        }
    }

    private ConnectionProfile BuildProfile() =>
        ConnectionProfile.Create(
            Id,
            Name,
            Uri,
            new AuthOptions
            {
                Kind = AuthKind,
                Username = Username,
                AuthDatabase = AuthDatabase,
            },
            tls: _tls,
            ssh: Ssh);
}
