using Serilog;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace MongoTui.TUI.Views;

/// <summary>
/// Wires dialog buttons so that activating them runs an action without letting the
/// default <c>Command.Accept</c> close the dialog. The dialog is closed explicitly
/// by the action when needed. Exceptions are logged instead of crashing the shell.
/// </summary>
internal static class DialogButtonExtensions
{
    /// <summary>
    /// Adds the buttons in the given order and restores the intended default: the button
    /// marked with <see cref="IAcceptTarget.IsDefault"/> before adding becomes the
    /// <see cref="View.DefaultAcceptView"/>. <see cref="Dialog.AddButton"/> otherwise
    /// promotes the last button added (usually Cancel/Close) to the default.
    /// </summary>
    public static void AddButtons(this Dialog dialog, params Button[] buttons)
    {
        Button? defaultButton = null;

        foreach (Button button in buttons)
        {
            if (button.IsDefault)
            {
                defaultButton = button;
            }

            dialog.AddButton(button);
        }

        if (defaultButton is null)
        {
            return;
        }

        foreach (Button button in dialog.Buttons)
        {
            button.IsDefault = ReferenceEquals(button, defaultButton);
        }

        dialog.DefaultAcceptView = defaultButton;
    }

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
                await action().ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                Log.Error("Dialog action failed ({ExceptionType})", exception.GetType().Name);
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
            Log.Error("Dialog action failed ({ExceptionType})", exception.GetType().Name);
        }
    }
}
