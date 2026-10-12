using MongoTui.Application.Abstractions.Ai;
using MongoTui.Application.Abstractions.Localization;
using MongoTui.Domain.Ai;
using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace MongoTui.TUI.Views;

/// <summary>Modal dialog that asks the AI assistant to draft a query.</summary>
public sealed class AiAssistantDialog : ThemedDialog
{
    private readonly IAiAssistant _assistant;
    private readonly ILocalizer _localizer;
    private readonly IApplication _application;
    private readonly QueryAssistantRequest _request;
    private readonly TextField _promptField = new();
#pragma warning disable CS0618 // TextView is superseded by the external Editor package; kept for M11.
    private readonly TextView _output = new() { Multiline = true, ReadOnly = true };
#pragma warning restore CS0618
    private readonly Label _error = new();

    public AiAssistantDialog(
        IAiAssistant assistant,
        ILocalizer localizer,
        IApplication application,
        QueryAssistantRequest request)
    {
        _assistant = assistant;
        _localizer = localizer;
        _application = application;
        _request = request;

        Title = localizer.Get("Ai.Title");
        Width = 76;
        Height = 22;

        Add(new Label { Text = localizer.Get("Ai.Field.Prompt"), X = 1, Y = 1 });
        _promptField.X = 12;
        _promptField.Y = 1;
        _promptField.Width = Dim.Fill();
        Add(_promptField);

        _output.X = 1;
        _output.Y = 3;
        _output.Width = Dim.Fill();
        _output.Height = Dim.Fill();
        Add(_output);

        _error.X = 1;
        _error.Y = Pos.AnchorEnd(3);
        _error.Width = Dim.Fill();
        Add(_error);

        var askButton = new Button { Text = localizer.Get("Ai.Ask"), IsDefault = true };
        askButton.OnAcceptAsync(AskAsync);
        var closeButton = new Button { Text = localizer.Get("Connections.Cancel") };
        closeButton.OnAccept(() => _application.RequestStop(this));

        AddButton(askButton);
        AddButton(closeButton);
    }

    private async Task AskAsync()
    {
        _error.Text = string.Empty;

        try
        {
            var request = _request with { Prompt = _promptField.Text ?? string.Empty };
            _output.Text = await _assistant.CompleteAsync(request, CancellationToken.None);
        }
        catch (Exception exception)
        {
            _error.Text = exception.Message;
        }
    }
}
