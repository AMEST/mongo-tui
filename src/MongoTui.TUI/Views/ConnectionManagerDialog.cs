using System.Collections.ObjectModel;
using MongoTui.Application.Abstractions.Localization;
using MongoTui.Application.Models;
using MongoTui.Application.Services;
using MongoTui.Domain.Connections;
using MongoTui.Domain.Exceptions;
using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace MongoTui.TUI.Views;

/// <summary>Modal dialog that lists and manages saved connections.</summary>
public sealed class ConnectionManagerDialog : ThemedDialog
{
    private readonly ConnectionService _connections;
    private readonly ILocalizer _localizer;
    private readonly IApplication _application;
    private readonly ListView _list = new();
    private readonly Label _status = new();

    private IReadOnlyList<ConnectionProfile> _profiles = [];
    private Button? _copyUriButton;

    public ConnectionManagerDialog(ConnectionService connections, ILocalizer localizer, IApplication application)
    {
        _connections = connections;
        _localizer = localizer;
        _application = application;

        Title = localizer.Get("Connections.Title");
        Width = 70;
        Height = 20;

        _list.X = 1;
        _list.Y = 1;
        _list.Width = Dim.Fill();
        _list.Height = Dim.Fill(6);
        _list.ValueChanged += (_, _) => UpdateCopyUriButton();
        Add(_list);

        _status.X = 1;
        _status.Y = Pos.AnchorEnd(3);
        _status.Width = Dim.Fill();
        Add(_status);

        AddActions();
    }

    private void AddActions()
    {
        _copyUriButton = new Button { Text = _localizer.Get("Connections.CopyUri"), X = 1, Y = Pos.AnchorEnd(5) };
        _copyUriButton.OnAcceptAsync(() => GuardedAsync(CopyUriAsync));
        Add(_copyUriButton);

        var connectButton = new Button { Text = _localizer.Get("Connections.Connect"), IsDefault = true };
        connectButton.OnAcceptAsync(ConnectAsync);
        var newButton = new Button { Text = _localizer.Get("Connections.New") };
        newButton.OnAcceptAsync(CreateAsync);
        var editButton = new Button { Text = _localizer.Get("Connections.Edit") };
        editButton.OnAcceptAsync(() => GuardedAsync(EditAsync));
        var deleteButton = new Button { Text = _localizer.Get("Connections.Delete") };
        deleteButton.OnAcceptAsync(() => GuardedAsync(DeleteAsync));
        var closeButton = new Button { Text = _localizer.Get("Common.Ok") };
        closeButton.OnAccept(() => _application.RequestStop(this));

        this.AddButtons(connectButton, newButton, editButton, deleteButton, closeButton);

        UpdateCopyUriButton();
    }

    private void UpdateCopyUriButton()
    {
        if (_copyUriButton is null)
        {
            return;
        }

        var hasSelection = SelectedIndex() is not null;
        _copyUriButton.Visible = hasSelection;
        _copyUriButton.Enabled = hasSelection;
    }

    /// <summary>The profile chosen for connection, if any.</summary>
    public ConnectionProfile? ConnectedProfile { get; private set; }

    /// <summary>The resolved target of the chosen connection, if any.</summary>
    public MongoConnectionTarget? ConnectedTarget { get; private set; }

    /// <summary>Reloads the connection list.</summary>
    public async Task LoadAsync()
    {
        _profiles = await _connections.GetProfilesAsync(CancellationToken.None);

        var names = _profiles.Count == 0
            ? new List<string> { _localizer.Get("Connections.Empty") }
            : _profiles.Select(profile => profile.Name).ToList();

        _list.SetSource(new ObservableCollection<string>(names));
        _list.SelectedItem = _profiles.Count > 0 ? 0 : null;
        UpdateCopyUriButton();
    }

    private int? SelectedIndex()
    {
        var selected = _list.SelectedItem;
        return selected is int index && index >= 0 && index < _profiles.Count ? index : null;
    }

    private async Task CreateAsync()
    {
        using var dialog = new ConnectionFormDialog(_connections, _localizer, _application, profile: null, secret: null);
        _application.Run(dialog);

        if (dialog.Saved)
        {
            await LoadAsync();
        }
    }

    private async Task EditAsync()
    {
        var index = SelectedIndex();
        if (index is null)
        {
            return;
        }

        var profile = _profiles[index.Value];
        var secret = await _connections.GetSecretAsync(profile.Id, CancellationToken.None);
        using var dialog = new ConnectionFormDialog(_connections, _localizer, _application, profile, secret);
        _application.Run(dialog);

        if (dialog.Saved)
        {
            await LoadAsync();
        }
    }

    private async Task ConnectAsync()
    {
        var index = SelectedIndex();
        if (index is null)
        {
            _status.Text = _localizer.Get("Connections.Empty");
            return;
        }

        var profile = _profiles[index.Value];

        try
        {
            ConnectedTarget = await _connections.ResolveTargetAsync(profile.Id, CancellationToken.None);
            ConnectedProfile = profile;
            _application.RequestStop(this);
        }
        catch (DomainException exception)
        {
            _status.Text = exception.Message;
            _application.Run(new ErrorDialog(_localizer, _application, _localizer.Get("Common.Error"), exception.Message));
        }
    }

    private async Task DeleteAsync()
    {
        var index = SelectedIndex();
        if (index is null)
        {
            return;
        }

        await _connections.DeleteAsync(_profiles[index.Value].Id, CancellationToken.None);
        await LoadAsync();
    }

    private async Task CopyUriAsync()
    {
        var index = SelectedIndex();
        if (index is null)
        {
            _status.Text = _localizer.Get("Connections.Empty");
            return;
        }

        var target = await _connections.ResolveTargetAsync(_profiles[index.Value].Id, CancellationToken.None);
        var copied = _application.Clipboard?.TrySetClipboardData(target.ConnectionString) == true;
        _status.Text = _localizer.Get(copied ? "Connections.UriCopied" : "Connections.CopyUriFailed");
    }

    private async Task GuardedAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (DomainException exception)
        {
            _status.Text = exception.Message;
        }
    }
}
