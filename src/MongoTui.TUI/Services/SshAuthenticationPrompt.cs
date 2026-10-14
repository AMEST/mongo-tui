using MongoTui.Application.Abstractions.Localization;
using MongoTui.Application.Abstractions.Ssh;
using MongoTui.TUI.Views;
using Terminal.Gui.App;

namespace MongoTui.TUI.Services;

public sealed class SshAuthenticationPrompt : ITwoFactorPrompt
{
    private readonly ILocalizer _localizer;

    public SshAuthenticationPrompt(ILocalizer localizer) => _localizer = localizer;

    public IApplication? Application { get; set; }

    public async Task<string?> RequestAsync(string prompt, CancellationToken cancellationToken)
    {
        var application = Application ?? throw new InvalidOperationException("The TUI is not running.");
        var completion = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellation = cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken));
        application.Invoke(() => ShowPrompt(application, prompt, completion, cancellationToken));
        return await completion.Task.ConfigureAwait(false);
    }

    private void ShowPrompt(
        IApplication application,
        string prompt,
        TaskCompletionSource<string?> completion,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        try
        {
            using var dialog = new SshAuthenticationDialog(_localizer, application, prompt);
            using var cancellation = cancellationToken.Register(() => application.Invoke(() => application.RequestStop(dialog)));
            application.Run(dialog);
            completion.TrySetResult(dialog.Response);
        }
        catch (Exception exception)
        {
            completion.TrySetException(exception);
        }
    }
}
