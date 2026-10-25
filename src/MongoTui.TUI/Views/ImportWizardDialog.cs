using MongoTui.Application.Localization;
using System.Collections.ObjectModel;
using MongoTui.Application.Abstractions.Localization;
using MongoTui.Application.Models;
using MongoTui.Application.Services;
using MongoTui.Domain.Collections;
using MongoTui.Domain.Exceptions;
using MongoTui.Domain.Transfer;
using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace MongoTui.TUI.Views;

/// <summary>Modal wizard that imports a collection via the CLI channel.</summary>
public sealed class ImportWizardDialog : ThemedDialog
{
    private readonly ImportService _import;
    private readonly ILocalizer _localizer;
    private readonly IApplication _application;
    private readonly MongoConnectionTarget _target;
    private readonly CollectionRef _collection;
    private readonly TextField _inputField = new();
    private readonly TextField _dropField = new();
    private readonly ListView _formatList = new();
    private readonly ListView _modeList = new();
    private readonly Label _error = new();
    private readonly string[] _formats = Enum.GetNames<ImportFormat>();
    private readonly string[] _modes = Enum.GetNames<ImportMode>();

    public ImportWizardDialog(
        ImportService import,
        ILocalizer localizer,
        IApplication application,
        MongoConnectionTarget target,
        CollectionRef collection)
    {
        _import = import;
        _localizer = localizer;
        _application = application;
        _target = target;
        _collection = collection;

        Title = localizer.Get(LocalizationKeys.Import.Title);
        Width = 72;
        Height = 18;

        Add(FieldLabel(LocalizationKeys.Import.FieldInput, 1));
        _inputField.X = 22;
        _inputField.Y = 1;
        _inputField.Width = Dim.Fill();
        Add(_inputField);

        Add(FieldLabel(LocalizationKeys.Transfer.FieldFormat, 3));
        _formatList.X = 22;
        _formatList.Y = 3;
        _formatList.Width = Dim.Fill();
        _formatList.Height = 3;
        _formatList.SetSource(new ObservableCollection<string>(_formats));
        _formatList.SelectedItem = Array.IndexOf(_formats, ImportFormat.Json.ToString());
        Add(_formatList);

        Add(FieldLabel(LocalizationKeys.Transfer.FieldMode, 7));
        _modeList.X = 22;
        _modeList.Y = 7;
        _modeList.Width = Dim.Fill();
        _modeList.Height = 4;
        _modeList.SetSource(new ObservableCollection<string>(_modes));
        _modeList.SelectedItem = Array.IndexOf(_modes, ImportMode.Insert.ToString());
        Add(_modeList);

        Add(FieldLabel(LocalizationKeys.Import.FieldDrop, 12));
        _dropField.X = 22;
        _dropField.Y = 12;
        _dropField.Width = 6;
        Add(_dropField);

        _error.X = 1;
        _error.Y = Pos.AnchorEnd(3);
        _error.Width = Dim.Fill();
        Add(_error);

        var runButton = new Button { Text = localizer.Get(LocalizationKeys.Transfer.Run), IsDefault = true };
        runButton.OnAcceptAsync(RunAsync);
        var cancelButton = new Button { Text = localizer.Get(LocalizationKeys.Connections.Cancel) };
        cancelButton.OnAccept(() => _application.RequestStop(this));

        this.AddButtons(runButton, cancelButton);
    }

    public bool Completed { get; private set; }

    private async Task RunAsync()
    {
        _error.Text = string.Empty;

        try
        {
            var format = Enum.Parse<ImportFormat>(_formats[_formatList.SelectedItem ?? 0]);
            var mode = Enum.Parse<ImportMode>(_modes[_modeList.SelectedItem ?? 0]);
            var drop = _dropField.Text?.Trim().StartsWith("y", StringComparison.OrdinalIgnoreCase) == true;

            var request = new ImportRequest
            {
                Collection = _collection,
                InputPath = _inputField.Text ?? string.Empty,
                Format = format,
                Mode = mode,
                DropCollection = drop,
            };

            await _import.ImportAsync(_target, request, CancellationToken.None);
            Completed = true;
            _application.RequestStop(this);
        }
        catch (DomainException exception)
        {
            _error.Text = exception is ProxyTransferNotSupportedException
                ? _localizer.Get(LocalizationKeys.Transfer.ProxyNotSupported)
                : exception.Message;
        }
    }

    private Label FieldLabel(string key, int y) => new() { Text = _localizer.Get(key), X = 1, Y = y };
}
