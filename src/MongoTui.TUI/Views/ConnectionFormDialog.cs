using System.Collections.ObjectModel;
using MongoTui.Application.Abstractions.Localization;
using MongoTui.Application.Services;
using MongoTui.Domain.Connections;
using MongoTui.TUI.ViewModels;
using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace MongoTui.TUI.Views;

/// <summary>Modal dialog that creates or edits a connection profile.</summary>
public sealed class ConnectionFormDialog : ThemedDialog
{
    private readonly ConnectionFormViewModel _viewModel;
    private readonly IApplication _application;
    private readonly ILocalizer _localizer;
    private readonly TextField _nameField = new();
    private readonly TextField _uriField = new();
    private readonly TextField _userField = new();
    private readonly TextField _passwordField = new() { Secret = true };
    private readonly TextField _authDatabaseField = new();
    private readonly ListView _authList = new();
    private readonly Label _error = new();
    private readonly string[] _authKinds = Enum.GetNames<AuthKind>();
    private CancellationTokenSource? _testCancellation;
    private bool _closing;

    public ConnectionFormDialog(
        ConnectionService connections,
        ILocalizer localizer,
        IApplication application,
        ConnectionProfile? profile,
        string? secret)
    {
        _application = application;
        _localizer = localizer;
        Disposing += (_, _) =>
        {
            _closing = true;
            _testCancellation?.Cancel();
        };
        _viewModel = new ConnectionFormViewModel(connections);

        if (profile is null)
        {
            _viewModel.LoadNew();
        }
        else
        {
            _viewModel.Load(profile, secret);
        }

        Title = localizer.Get("Connections.Title");
        Width = 80;
        Height = 20;

        Add(FieldLabel(localizer.Get("Connections.Field.Name"), 1));
        _nameField.X = 22;
        _nameField.Y = 1;
        _nameField.Width = Dim.Fill();
        Add(_nameField);

        Add(FieldLabel(localizer.Get("Connections.Field.Uri"), 3));
        _uriField.X = 22;
        _uriField.Y = 3;
        _uriField.Width = Dim.Fill();
        Add(_uriField);

        Add(FieldLabel(localizer.Get("Connections.Field.Username"), 5));
        _userField.X = 22;
        _userField.Y = 5;
        _userField.Width = Dim.Fill();
        Add(_userField);

        Add(FieldLabel(localizer.Get("Connections.Field.Password"), 7));
        _passwordField.X = 22;
        _passwordField.Y = 7;
        _passwordField.Width = Dim.Fill();
        Add(_passwordField);

        Add(FieldLabel(localizer.Get("Connections.Field.Auth"), 9));
        _authList.X = 22;
        _authList.Y = 9;
        _authList.Width = Dim.Fill();
        _authList.Height = 4;
        _authList.SetSource(new ObservableCollection<string>(_authKinds));
        Add(_authList);

        Add(FieldLabel(localizer.Get("Connections.Field.AuthDatabase"), 14));
        _authDatabaseField.X = 22;
        _authDatabaseField.Y = 14;
        _authDatabaseField.Width = Dim.Fill();
        Add(_authDatabaseField);

        _error.X = 1;
        _error.Y = Pos.AnchorEnd(3);
        _error.Width = Dim.Fill();
        Add(_error);

        PopulateFromViewModel();

        var fromUriButton = new Button { Text = localizer.Get("Connections.FromUri") };
        fromUriButton.OnAccept(ApplyFromUri);
        var saveButton = new Button { Text = localizer.Get("Connections.Save"), IsDefault = true };
        saveButton.OnAcceptAsync(SaveAsync);
        var testButton = new Button { Text = localizer.Get("Connections.Test") };
        testButton.OnAcceptAsync(TestAsync);
        var cancelButton = new Button { Text = localizer.Get("Connections.Cancel") };
        cancelButton.OnAccept(() => _application.RequestStop(this));
        var sshButton = new Button { Text = localizer.Get("Ssh.Settings") };
        sshButton.OnAccept(EditSsh);

        this.AddButtons(fromUriButton, sshButton, saveButton, testButton, cancelButton);
    }

    /// <summary>Whether the profile was saved before the dialog closed.</summary>
    public bool Saved { get; private set; }

    private static Label FieldLabel(string text, int y) => new() { Text = text, X = 1, Y = y };

    private void PopulateFromViewModel()
    {
        _nameField.Text = _viewModel.Name;
        _uriField.Text = _viewModel.Uri;
        _userField.Text = _viewModel.Username ?? string.Empty;
        _passwordField.Text = _viewModel.Password ?? string.Empty;
        _authDatabaseField.Text = _viewModel.AuthDatabase ?? string.Empty;
        _authList.SelectedItem = Array.IndexOf(_authKinds, _viewModel.AuthKind.ToString());
    }

    private void ApplyFromUri()
    {
        _error.Text = string.Empty;

        if (_viewModel.ApplyUri(_uriField.Text ?? string.Empty))
        {
            PopulateFromViewModel();
        }
        else
        {
            _error.Text = _viewModel.ValidationError ?? string.Empty;
        }
    }

    private void SyncFromFields()
    {
        _viewModel.Name = _nameField.Text ?? string.Empty;
        _viewModel.Uri = _uriField.Text ?? string.Empty;
        _viewModel.Username = NullIfEmpty(_userField.Text);
        _viewModel.Password = NullIfEmpty(_passwordField.Text);
        _viewModel.AuthDatabase = NullIfEmpty(_authDatabaseField.Text);

        var index = _authList.SelectedItem ?? 0;
        _viewModel.AuthKind = Enum.Parse<AuthKind>(_authKinds[index]);
    }

    private async Task SaveAsync()
    {
        SyncFromFields();

        if (await _viewModel.SaveAsync(CancellationToken.None))
        {
            Saved = true;
            _application.RequestStop(this);
        }
        else
        {
            _error.Text = _viewModel.ValidationError ?? string.Empty;
            PopulateFromViewModel();
        }
    }

    private async Task TestAsync()
    {
        if (_testCancellation is not null)
        {
            return;
        }

        SyncFromFields();
        using var cancellation = new CancellationTokenSource();
        _testCancellation = cancellation;
        try
        {
            var error = await _viewModel.TestAsync(cancellation.Token);
            if (!_closing)
            {
                PopulateFromViewModel();
            }

            SetTestStatus(error ?? _localizer.Get("Common.Ok"));
        }
        catch (OperationCanceledException)
        {
            SetTestStatus(_localizer.Get("Ssh.Cancelled"));
        }
        finally
        {
            _testCancellation = null;
        }
    }

    private void SetTestStatus(string message)
    {
        if (!_closing)
        {
            _error.Text = message;
        }
    }

    private void EditSsh()
    {
        using var dialog = new SshTunnelDialog(_localizer, _application, _viewModel.Ssh);
        _application.Run(dialog);
        if (dialog.Options is { } options)
        {
            _viewModel.Ssh = options;
        }
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;
}
