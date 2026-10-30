using MongoTui.Application.Localization;
using MongoTui.Application.Services;
using Terminal.Gui.Drawing;
using Terminal.Gui.Views;

namespace MongoTui.TUI.Views;

public sealed partial class ConnectionFormDialog
{
    private Task SaveAsync() => RunAsync(false);
    private Task TestAsync() => RunAsync(true);

    private async Task RunAsync(bool test)
    {
        if (_operation is not null || _closing) return;
        SyncFromFields();
        using var operation = new CancellationTokenSource();
        if (test) operation.CancelAfter(TimeSpan.FromSeconds(ConnectionService.TestTimeoutSeconds));
        _operation = operation;
        SetBusy(true);
        SetStatus(_localizer.Get(test ? LocalizationKeys.Connections.TestRunning : LocalizationKeys.Common.Working), ColorName16.BrightYellow);
        string? error = null;
        try
        {
            if (test) error = await _viewModel.TestAsync(operation.Token).ConfigureAwait(false);
            else if (!await _viewModel.SaveAsync(operation.Token).ConfigureAwait(false)) error = _viewModel.ValidationError;
        }
        catch (OperationCanceledException)
        {
            error = test && operation.IsCancellationRequested
                ? _localizer.Format(LocalizationKeys.Connections.TestTimeout, ConnectionService.TestTimeoutSeconds)
                : _localizer.Get(LocalizationKeys.Ssh.Cancelled);
        }
        catch (Exception exception) { error = exception.Message; }
        if (_closing) return;
        await _application.InvokeAsync(() => CompleteOperation(test, error)).ConfigureAwait(false);
    }

    private void CompleteOperation(bool test, string? error)
    {
        if (_closing) return;
        _operation = null;
        SetBusy(false);
        PopulateFromViewModel();
        if (test) SetTestStatus(error);
        else if (error is not null) SetStatus(error, ColorName16.BrightRed);
        else
        {
            Saved = true;
            _application.RequestStop(this);
        }
    }

    private void SetBusy(bool busy)
    {
        foreach (var field in SubViews.Where(view => view is TextField or ListView)) field.Enabled = !busy;
        foreach (var button in Buttons.Where(button => button.Text != _localizer.Get(LocalizationKeys.Connections.Cancel)))
            button.Enabled = !busy;
    }
}
