using MongoTui.Application.Abstractions.Ai;
using MongoTui.Application.Abstractions.Localization;
using MongoTui.Domain.Ai;
using Terminal.Gui.App;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace MongoTui.TUI.Views;

/// <summary>Modal dialog that asks the AI assistant to draft a query.</summary>
public sealed class AiAssistantDialog : ThemedDialog
{
    // A modal dialog runs its own loop, which does not resume awaited continuations until the next
    // input event. Polling lets the response be applied as soon as it arrives (#issue).
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(100);

    private readonly IAiAssistant _assistant;
    private readonly ILocalizer _localizer;
    private readonly IApplication _application;
    private readonly QueryAssistantRequest _request;
    private readonly TextField _promptField = new();
#pragma warning disable CS0618 // TextView is superseded by the external Editor package; kept for M11.
    private readonly TextView _output = new() { Multiline = true, ReadOnly = true };
#pragma warning restore CS0618
    private readonly Label _status = new();
    private readonly Button _askButton;
    private CancellationTokenSource? _running;
    private bool _closing;

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
        Disposing += (_, _) =>
        {
            _closing = true;
            var running = _running;
            _running = null;
            running?.Cancel();
            running?.Dispose();
        };

        Title = localizer.Get("Ai.Title");
        Width = 76;
        Height = 22;

        Add(new Label { Text = localizer.Get("Ai.Field.Prompt"), X = 1, Y = 1 });
        _promptField.X = 12;
        _promptField.Y = 1;
        _promptField.Width = Dim.Fill();
        _promptField.KeyDown += OnPromptKeyDown;
        Add(_promptField);

        _output.X = 1;
        _output.Y = 3;
        _output.Width = Dim.Fill();
        _output.Height = Dim.Fill();
        Add(_output);

        _status.X = 1;
        _status.Y = Pos.AnchorEnd(3);
        _status.Width = Dim.Fill();
        Add(_status);

        _askButton = new Button { Text = localizer.Get("Ai.Ask"), IsDefault = true };
        _askButton.OnAccept(AskAsync);
        var closeButton = new Button { Text = localizer.Get("Connections.Cancel") };
        closeButton.OnAccept(() => _application.RequestStop(this));

        this.AddButtons(_askButton, closeButton);
        _promptField.SetFocus();
    }

    /// <summary>Whether a request is currently in flight (exposed for tests).</summary>
    internal bool IsBusy => _running is not null;

    /// <summary>The status line text (exposed for tests).</summary>
    internal string StatusText => _status.Text ?? string.Empty;

    // Enter in the prompt must run the request instead of closing the dialog through Command.Accept.
    private void OnPromptKeyDown(object? sender, Key key)
    {
        if (key == Key.Enter)
        {
            key.Handled = true;
            AskAsync();
        }
    }

    internal void AskAsync()
    {
        if (_running is not null)
        {
            return;
        }

        var cancellation = new CancellationTokenSource();
        _running = cancellation;
        _askButton.Enabled = false;
        SetStatus(_localizer.Get("Ai.Running"));
        SetNeedsDraw();

        var request = _request with { Prompt = _promptField.Text ?? string.Empty };

        // Run off the UI thread so the HTTP continuations do not bind to the modal loop's
        // synchronization context, which does not resume them until the next input event.
        var task = Task.Run(() => _assistant.CompleteAsync(request, cancellation.Token));
        _ = task.ContinueWith(static completed => _ = completed.Exception, TaskScheduler.Default);
        _application.AddTimeout(PollInterval, () => Poll(task, cancellation));
    }

    // Runs on the dialog's main loop until the task completes.
    internal bool Poll(Task<string> task, CancellationTokenSource cancellation)
    {
        if (!ReferenceEquals(_running, cancellation))
        {
            return false;
        }

        if (!task.IsCompleted)
        {
            return true;
        }

        Complete(cancellation);

        if (task.IsFaulted)
        {
            SetStatus(task.Exception!.GetBaseException().Message);
        }
        else if (!task.IsCanceled)
        {
            SetStatus(string.Empty);
            _output.Text = task.Result;
        }

        SetNeedsDraw();

        return false;
    }

    private void Complete(CancellationTokenSource cancellation)
    {
        if (ReferenceEquals(_running, cancellation))
        {
            _running = null;
        }

        cancellation.Dispose();
        _askButton.Enabled = true;
    }

    private void SetStatus(string text)
    {
        if (!_closing)
        {
            _status.Text = text;
        }
    }
}
