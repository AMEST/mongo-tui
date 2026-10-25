using AwesomeAssertions;
using MongoTui.TUI.Views;
using Terminal.Gui.Input;
using Terminal.Gui.Views;

namespace MongoTui.TUI.Tests.Views;

public sealed class DialogButtonAsyncTests
{
    [Fact]
    public async Task Should_NotPostToUiContext_When_ActionFinishesAfterDialogClosed()
    {
        var response = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var context = new CompletionContext();
        using var button = new Button();
        button.OnAcceptAsync(() => response.Task);
        var previous = SynchronizationContext.Current;
        try
        {
            SynchronizationContext.SetSynchronizationContext(context);
            button.InvokeCommand(Command.Accept);
        }
        finally { SynchronizationContext.SetSynchronizationContext(previous); }

        response.SetResult();
        await context.Completed.Task.WaitAsync(TimeSpan.FromSeconds(3));

        context.Posts.Should().Be(0);
    }

    private sealed class CompletionContext : SynchronizationContext
    {
        public TaskCompletionSource Completed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Posts;
        public override void OperationCompleted() => Completed.TrySetResult();
        public override void Post(SendOrPostCallback callback, object? state)
        {
            Interlocked.Increment(ref Posts);
            ThreadPool.QueueUserWorkItem(_ => callback(state));
        }
    }
}
