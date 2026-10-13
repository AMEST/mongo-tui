using System.Collections.ObjectModel;
using MongoTui.Application.Abstractions.Localization;
using MongoTui.Application.Services;
using MongoTui.Domain.Ai;
using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace MongoTui.TUI.Views;

/// <summary>Modal dialog that translates a foreign query into MongoDB via the AI assistant.</summary>
public sealed class TranslateDialog : ThemedDialog
{
    private readonly TranslateQueryService _translate;
    private readonly IApplication _application;
#pragma warning disable CS0618 // TextView is superseded by the external Editor package; kept for M12.
    private readonly TextView _source = new() { Multiline = true };
    private readonly TextView _output = new() { Multiline = true, ReadOnly = true };
#pragma warning restore CS0618
    private readonly ListView _modeList = new();
    private readonly Label _error = new();
    private readonly string[] _modes = Enum.GetNames<QueryAssistantMode>();

    public TranslateDialog(TranslateQueryService translate, ILocalizer localizer, IApplication application)
    {
        _translate = translate;
        _application = application;

        Title = localizer.Get("Translate.Title");
        Width = 76;
        Height = 24;

        Add(new Label { Text = localizer.Get("Translate.Field.Source"), X = 1, Y = 1 });
        _source.X = 1;
        _source.Y = 2;
        _source.Width = Dim.Fill();
        _source.Height = 6;
        Add(_source);

        Add(new Label { Text = localizer.Get("Translate.Field.Target"), X = 1, Y = 9 });
        _modeList.X = 1;
        _modeList.Y = 10;
        _modeList.Width = Dim.Fill();
        _modeList.Height = 3;
        _modeList.SetSource(new ObservableCollection<string>(_modes));
        _modeList.SelectedItem = 0;
        Add(_modeList);

        _output.X = 1;
        _output.Y = 14;
        _output.Width = Dim.Fill();
        _output.Height = Dim.Fill();
        Add(_output);

        _error.X = 1;
        _error.Y = Pos.AnchorEnd(3);
        _error.Width = Dim.Fill();
        Add(_error);

        var translateButton = new Button { Text = localizer.Get("Translate.Run"), IsDefault = true };
        translateButton.OnAcceptAsync(TranslateAsync);
        var cancelButton = new Button { Text = localizer.Get("Connections.Cancel") };
        cancelButton.OnAccept(() => _application.RequestStop(this));

        this.AddButtons(translateButton, cancelButton);
    }

    private async Task TranslateAsync()
    {
        _error.Text = string.Empty;

        try
        {
            var mode = Enum.Parse<QueryAssistantMode>(_modes[_modeList.SelectedItem ?? 0]);
            _output.Text = await _translate.TranslateToMongoAsync(
                _source.Text ?? string.Empty,
                mode,
                CancellationToken.None);
        }
        catch (Exception exception)
        {
            _error.Text = exception.Message;
        }
    }
}
