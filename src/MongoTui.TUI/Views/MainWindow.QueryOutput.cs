using MongoTui.Application.Localization;
using MongoTui.Application.Models;
using Terminal.Gui.Views;

namespace MongoTui.TUI.Views;

public sealed partial class MainWindow
{
    private async Task SaveQueryOutputAsync()
    {
        if (Application is not { } application
            || _queryTabs.ActiveTab is not { IsExecuting: false, Result.Output: { Length: > 0 } output }) return;

        using var dialog = new SaveDialog
        {
            Title = _localizer.Get(LocalizationKeys.Result.SaveOutput),
            Path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "query-output.txt"),
            OpenMode = OpenMode.File,
        };
        application.Run(dialog);
        if (dialog.Canceled || dialog.FileName is not { Length: > 0 } path) return;

        var cancellationToken = _documentLifetime.Token;
        string message;
        try
        {
            await SaveOutputAsync(output, path, cancellationToken).ConfigureAwait(false);
            message = _localizer.Format(LocalizationKeys.Result.OutputSaved, path);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return; }
        catch (Exception exception) { message = exception.Message; }

        if (!cancellationToken.IsCancellationRequested)
            await application.InvokeAsync(() =>
            {
                if (!cancellationToken.IsCancellationRequested) SetStatus(message);
            }).ConfigureAwait(false);
    }

    private static async Task SaveOutputAsync(ScriptOutput output, string path, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path))!;
        var temporary = Path.Combine(directory, $".mongotui-output-{Guid.NewGuid():N}.tmp");
        var options = new FileStreamOptions
        {
            Mode = FileMode.CreateNew, Access = FileAccess.Write, Options = FileOptions.Asynchronous,
        };
        if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;

        await using var source = await output.OpenReadAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using (var destination = new FileStream(temporary, options))
                await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporary, path);
        }
        finally { File.Delete(temporary); }
    }
}
