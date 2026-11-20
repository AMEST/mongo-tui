using MongoTui.Application.Abstractions.Localization;
using MongoTui.Domain.Exceptions;
using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace MongoTui.TUI.Views;

public abstract class DiagnosticDialogBase : ThemedDialog
{
    protected readonly ILocalizer Localizer;
    protected readonly IApplication UiApplication;
    protected readonly CancellationTokenSource Lifetime = new();
    protected readonly Label Feedback = new() { X = 1, Y = Pos.AnchorEnd(3), Width = Dim.Fill(1) };
    private readonly List<Button> _actions = [];
    protected bool Busy;
    protected volatile bool Closed;
    protected DiagnosticDialogBase(ILocalizer localizer, IApplication application, string title)
    {
        Localizer = localizer; UiApplication = application; Title = localizer.Get(title);
        Width = Dim.Percent(95); Height = Dim.Percent(90);
        Add(Feedback);
        var close = new Button { Text = localizer.Get("Common.Close") };
        close.OnAccept(() => application.RequestStop(this)); this.AddButtons(close);
        Disposing += (_, _) => { Closed = true; Lifetime.Cancel(); Lifetime.Dispose(); };
    }
    protected Button ActionButton(string key, Func<Task> action, bool footer = true)
    {
        var button = new Button { Text = Localizer.Get(key) };
        button.OnAcceptAsync(action); _actions.Add(button);
        if (footer) this.AddButtons(button); else Add(button);
        foreach (var candidate in Buttons) candidate.IsDefault = false;
        _actions[0].IsDefault = true; DefaultAcceptView = _actions[0];
        return button;
    }
    protected async Task RunAsync<T>(Func<CancellationToken, Task<T>> request, Action<T> apply, Action? completed = null)
    {
        if (Busy || Closed) return;
        Busy = true; foreach (var button in _actions) button.Enabled = false;
        Feedback.Text = Localizer.Get("Common.Loading");
        T? result = default; Exception? error = null;
        try { result = await request(Lifetime.Token).ConfigureAwait(false); }
        catch (Exception exception) { error = exception; }
        if (Closed) return;
        await UiApplication.InvokeAsync(() =>
        {
            if (Closed) return;
            Busy = false; foreach (var button in _actions) button.Enabled = true;
            Feedback.Text = error is null ? "" : Localizer.Get(error is DiagnosticsException diagnostic ? diagnostic.Key
                : error is QueryTimeoutException ? "Query.TimedOut" : error is OperationCanceledException ? "Query.Cancelled" : "Diagnostics.Error.Command");
            if (error is null) apply(result!);
            completed?.Invoke();
            SetNeedsDraw();
        }).ConfigureAwait(false);
    }
}
