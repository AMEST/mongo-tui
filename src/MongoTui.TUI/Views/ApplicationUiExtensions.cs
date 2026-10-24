using Terminal.Gui.App;

namespace MongoTui.TUI.Views;

internal static class ApplicationUiExtensions
{
    // Terminal.Gui 2.5 runs timer callbacks under its timer lock. A nested Run
    // holds that lock, blocking background Invoke/Post calls. Iteration bypasses it.
    public static Task InvokeAsync(this IApplication application, Action action)
    {
        if (application.MainThreadId == Environment.CurrentManagedThreadId)
        {
            action();
            return Task.CompletedTask;
        }

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        application.Iteration += OnIteration;
        return completion.Task;

        void OnIteration(object? sender, Terminal.Gui.App.EventArgs<IApplication?> args)
        {
            application.Iteration -= OnIteration;
            try
            {
                action();
                completion.SetResult();
            }
            catch (Exception exception)
            {
                completion.SetException(exception);
            }
        }
    }
}
