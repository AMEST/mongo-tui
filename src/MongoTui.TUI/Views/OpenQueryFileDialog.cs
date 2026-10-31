using System.Collections.ObjectModel;
using MongoTui.Application.Localization;
using MongoTui.Application.Abstractions.Localization;
using MongoTui.Application.Services;
using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace MongoTui.TUI.Views;

/// <summary>Chooses a query file from the recent list, the file system or a typed path.</summary>
public sealed class OpenQueryFileDialog : ThemedDialog
{
    private readonly QueryFileService _files;
    private readonly ILocalizer _localizer;
    private readonly IApplication _application;
    private readonly ListView _recent = new() { X = 1, Y = 1, Width = Dim.Fill(1), Height = Dim.Percent(45) };
    private readonly TextField _path = new() { Width = Dim.Fill(1) };
    private readonly Label _status = new() { X = 1, Y = Pos.AnchorEnd(3), Width = Dim.Fill(1) };
    private readonly CancellationTokenSource _lifetime = new();
    private IReadOnlyList<string> _recentPaths = [];
    private bool _disposed;

    public OpenQueryFileDialog(QueryFileService files, ILocalizer localizer, IApplication application)
    {
        _files = files;
        _localizer = localizer;
        _application = application;
        Title = localizer.Get(LocalizationKeys.QueryFiles.OpenTitle);
        Width = Dim.Percent(85);
        Height = Dim.Percent(85);

        Add(new Label { Text = localizer.Get(LocalizationKeys.QueryFiles.Recent), X = 1, Y = 0 }, _recent);
        var pathLabel = new Label { Text = localizer.Get(LocalizationKeys.QueryFiles.Path), X = 1, Y = Pos.Bottom(_recent) };
        _path.X = 1;
        _path.Y = Pos.Bottom(pathLabel);
        Add(pathLabel, _path, _status);
        _recent.ValueChanged += (_, _) => SelectRecent();
        _recent.Accepting += (_, args) => { args.Handled = true; Open(); };
        AddActions();
        Disposing += (_, _) => { _disposed = true; _lifetime.Cancel(); _lifetime.Dispose(); };
    }

    /// <summary>The file path chosen for opening, if any.</summary>
    public string? SelectedPath { get; private set; }

    /// <summary>Loads the recent file list into the dialog.</summary>
    public async Task LoadAsync()
    {
        if (_disposed) return;
        _status.Text = _localizer.Get(LocalizationKeys.Common.Loading);
        IReadOnlyList<string>? recent = null;
        try { recent = await _files.GetRecentAsync(_lifetime.Token).ConfigureAwait(false); }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { return; }
        catch { }
        if (_disposed || _lifetime.IsCancellationRequested) return;
        await _application.InvokeAsync(() =>
        {
            if (_disposed || _lifetime.IsCancellationRequested) return;
            _recentPaths = recent ?? [];
            _recent.SetSource(new ObservableCollection<string>(_recentPaths.Count == 0
                ? [_localizer.Get(LocalizationKeys.QueryFiles.Empty)] : _recentPaths));
            _recent.SelectedItem = _recentPaths.Count > 0 ? 0 : null;
            if (_recentPaths.Count > 0) _path.Text = _recentPaths[0];
            _status.Text = string.Empty;
        }).ConfigureAwait(false);
    }

    private void AddActions()
    {
        var browse = new Button { Text = _localizer.Get(LocalizationKeys.QueryFiles.Browse) };
        browse.OnAccept(Browse);
        var open = new Button { Text = _localizer.Get(LocalizationKeys.QueryFiles.Open), IsDefault = true };
        open.OnAccept(Open);
        var cancel = new Button { Text = _localizer.Get(LocalizationKeys.Common.Cancel) };
        cancel.OnAccept(() => { _lifetime.Cancel(); _application.RequestStop(this); });
        this.AddButtons(browse, open, cancel);
    }

    private void Browse()
    {
        using var dialog = new OpenDialog
        {
            Title = _localizer.Get(LocalizationKeys.QueryFiles.OpenTitle),
            Path = _path.Text ?? string.Empty,
            OpenMode = OpenMode.File,
            MustExist = true,
            AllowsMultipleSelection = false,
            AllowedTypes = [new AllowedType(_localizer.Get(LocalizationKeys.QueryFiles.JavaScript), ".js")],
        };
        _application.Run(dialog);
        if (!dialog.Canceled && dialog.Path is { Length: > 0 } selected) _path.Text = selected;
    }

    private void SelectRecent()
    {
        if (_recent.SelectedItem is int index && index >= 0 && index < _recentPaths.Count) _path.Text = _recentPaths[index];
    }

    private void Open()
    {
        var path = (_path.Text ?? string.Empty).Trim();
        if (path.Length == 0)
        {
            _status.Text = _localizer.Get(LocalizationKeys.QueryFiles.PathRequired);
            return;
        }

        SelectedPath = path;
        _lifetime.Cancel();
        _application.RequestStop(this);
    }
}
