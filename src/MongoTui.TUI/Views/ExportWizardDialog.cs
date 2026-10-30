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

/// <summary>Modal wizard that exports a collection via the CLI channel.</summary>
public sealed class ExportWizardDialog : ThemedDialog
{
    private readonly ExportService _export;
    private readonly ILocalizer _localizer;
    private readonly IApplication _application;
    private readonly MongoConnectionTarget _target;
    private readonly CollectionRef _collection;
    private readonly TextField _outputField = new();
    private readonly TextField _filterField = new();
    private readonly TextField _limitField = new();
    private readonly ListView _formatList = new();
    private readonly Label _error = new();
    private readonly string[] _formats = Enum.GetNames<ExportFormat>();

    public ExportWizardDialog(
        ExportService export,
        ILocalizer localizer,
        IApplication application,
        MongoConnectionTarget target,
        CollectionRef collection)
    {
        _export = export;
        _localizer = localizer;
        _application = application;
        _target = target;
        _collection = collection;

        Title = localizer.Get(LocalizationKeys.Export.Title);
        Width = 72;
        Height = 18;

        Add(FieldLabel(LocalizationKeys.Export.FieldOutput, 1));
        _outputField.X = 22;
        _outputField.Y = 1;
        _outputField.Width = Dim.Fill();
        Add(_outputField);

        Add(FieldLabel(LocalizationKeys.Export.FieldFilter, 3));
        _filterField.X = 22;
        _filterField.Y = 3;
        _filterField.Width = Dim.Fill();
        Add(_filterField);

        Add(FieldLabel(LocalizationKeys.Export.FieldLimit, 5));
        _limitField.X = 22;
        _limitField.Y = 5;
        _limitField.Width = 12;
        Add(_limitField);

        Add(FieldLabel(LocalizationKeys.Transfer.FieldFormat, 7));
        _formatList.X = 22;
        _formatList.Y = 7;
        _formatList.Width = Dim.Fill();
        _formatList.Height = 5;
        _formatList.SetSource(new ObservableCollection<string>(_formats));
        _formatList.SelectedItem = Array.IndexOf(_formats, ExportFormat.CanonicalJson.ToString());
        Add(_formatList);

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
            var format = Enum.Parse<ExportFormat>(_formats[_formatList.SelectedItem ?? 0]);
            int? limit = int.TryParse(_limitField.Text, out var parsed) ? parsed : null;

            var request = new ExportRequest
            {
                Collection = _collection,
                OutputPath = _outputField.Text ?? string.Empty,
                Format = format,
                FilterJson = string.IsNullOrWhiteSpace(_filterField.Text) ? null : _filterField.Text,
                Limit = limit,
            };

            await _export.ExportAsync(_target, request, CancellationToken.None);
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
