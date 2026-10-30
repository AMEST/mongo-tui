using MongoTui.Application.Localization;
using MongoTui.Application.Abstractions.Localization;
using MongoTui.Application.Services;
using MongoTui.Application.Models;
using MongoTui.Domain.Connections;
using MongoTui.Domain.Exceptions;
using MongoTui.Domain.Primitives;
using MongoTui.TUI.Localization;

namespace MongoTui.TUI.ViewModels;

/// <summary>Backs the create/edit connection dialog.</summary>
public sealed class ConnectionFormViewModel : ObservableObject
{
    private readonly ConnectionService _connections;
    private readonly ILocalizer _localizer;

    private string _name = string.Empty;
    private string _uri = "mongodb://localhost:27017";
    private AuthKind _authKind = AuthKind.None;
    private string? _username;
    private string? _password;
    private string? _authDatabase;
    private string? _validationError;
    private SshTunnelOptions _ssh = new();
    private ConnectionProfile? _original;
    private string? _originalSecret;

    public ConnectionFormViewModel(ConnectionService connections, ILocalizer localizer)
    {
        _connections = connections;
        _localizer = localizer;
    }

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
        _original = null;
        _originalSecret = null;
        OnPropertyChanged(nameof(Id));
        OnPropertyChanged(nameof(IsNew));
    }

    public void Load(ConnectionProfile profile, string? secret)
    {
        (profile, secret) = ConnectionCredentials.Extract(profile, secret);
        Id = profile.Id;
        IsNew = false;
        Name = profile.Name;
        Uri = profile.Uri;
        AuthKind = profile.Auth.Kind;
        Username = profile.Auth.Username;
        AuthDatabase = profile.Auth.AuthDatabase;
        Password = secret;
        Ssh = profile.Ssh;
        _original = profile;
        _originalSecret = secret;
        ValidationError = null;
        OnPropertyChanged(nameof(Id));
        OnPropertyChanged(nameof(IsNew));
    }

    public async Task<bool> SaveAsync(CancellationToken cancellationToken)
    {
        ValidationError = null;
        try
        {
            var (profile, secret) = Prepare(Name);
            await _connections.SaveAsync(profile, secret, cancellationToken).ConfigureAwait(false);
            _original = profile;
            _originalSecret = secret;
            IsNew = false;
            OnPropertyChanged(nameof(IsNew));

            return true;
        }
        catch (DomainException exception)
        {
            ValidationError = _localizer.GetErrorMessage(exception);
            return false;
        }
    }

    public async Task<string?> TestAsync(CancellationToken cancellationToken)
    {
        ValidationError = null;
        try
        {
            var (profile, secret) = Prepare(_localizer.Get(LocalizationKeys.Connections.Test));
            await _connections.TestConnectionAsync(profile, secret, cancellationToken).ConfigureAwait(false);

            return null;
        }
        catch (DomainException exception)
        {
            ValidationError = _localizer.GetErrorMessage(exception);
            return ValidationError;
        }
    }

    /// <summary>
    /// Removes URI credentials and fills empty login/password fields while preserving typed values.
    /// </summary>
    internal void MergeCredentialsFromUri()
    {
        try
        {
            var parsed = ConnectionUriParser.Parse(Uri);
            if (parsed.Username is null && parsed.Password is null) return;

            Uri = parsed.Uri;
            var (auth, secret) = ConnectionCredentials.Merge(parsed, ReadAuth(), Password);
            ApplyCredentials(auth, secret);
        }
        catch (DomainException exception)
        {
            ValidationError = _localizer.GetErrorMessage(exception);
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
            ValidationError = _localizer.GetErrorMessage(exception);
            return false;
        }
    }

    private (ConnectionProfile Profile, string? Secret) Prepare(string name)
    {
        var draft = new ConnectionProfileDraft { Id = Id, Name = name, Uri = Uri,
            Auth = ReadAuth(), Password = Password, Ssh = Ssh };
        var prepared = ConnectionProfilePreparation.Prepare(draft, _original, _originalSecret);
        Uri = prepared.Profile.Uri;
        ApplyCredentials(prepared.Profile.Auth, prepared.Secret);
        return prepared;
    }

    private AuthOptions ReadAuth() => new() { Kind = AuthKind, Username = Username, AuthDatabase = AuthDatabase };

    private void ApplyCredentials(AuthOptions auth, string? secret)
    {
        AuthKind = auth.Kind;
        Username = auth.Username;
        AuthDatabase = auth.AuthDatabase;
        Password = secret;
    }
}
