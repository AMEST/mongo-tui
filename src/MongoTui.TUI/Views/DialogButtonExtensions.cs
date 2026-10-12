using Serilog;
using Terminal.Gui.Views;

namespace MongoTui.TUI.Views;

/// <summary>
/// Wires dialog buttons so that activating them runs an action without letting the
/// default <c>Command.Accept</c> close the dialog. The dialog is closed explicitly
/// by the action when needed. Exceptions are logged instead of crashing the shell.
/// </summary>
internal static class DialogButtonExtensions
{
    public static void OnAccept(this Button button, Action action) =>
        button.Accepting += (_, args) =>
        {
            args.Handled = true;
            Guard(action);
        };

    public static void OnAcceptAsync(this Button button, Func<Task> action) =>
        button.Accepting += async (_, args) =>
        {
            args.Handled = true;

            try
            {
                await action();
            }
            catch (Exception exception)
            {
                Log.Error(exception, "Dialog action failed");
            }
        };

    private static void Guard(Action action)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Dialog action failed");
        }
    }
}
