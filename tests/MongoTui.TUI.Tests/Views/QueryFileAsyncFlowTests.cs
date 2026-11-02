using System.Diagnostics;
using System.Reflection;
using AwesomeAssertions;
using FakeItEasy;
using MongoTui.Application.Localization;
using MongoTui.Application.Models;
using MongoTui.Domain.Primitives;
using MongoTui.TUI.Views;
using Terminal.Gui.Input;
using Terminal.Gui.Views;

namespace MongoTui.TUI.Tests.Views;

public sealed class QueryFileAsyncFlowTests
{
    [Theory]
    [InlineData("en", "success")]
    [InlineData("ru", "success")]
    [InlineData("en", "error")]
    [InlineData("ru", "error")]
    [InlineData("en", "cancel")]
    [InlineData("ru", "cancel")]
    public async Task Should_CompleteSave_WhenIoFinishesInsideNestedTimerDialog(string language, string outcome)
    {
        await using var host = new TuiFeatureTestHost(language);
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var dialog = new SaveQueryFileDialog(host.Localizer, host.App, "/tmp/query.js", _ => completion.Task);
        var observation = new SaveObservation(host, dialog, completion, outcome);

        host.App.Run(host.Window);

        observation.BusyVisible.Should().BeTrue();
        dialog.Buttons.Should().OnlyContain(button => button.Enabled);
        dialog.Saved.Should().Be(outcome == "success");
        observation.ErrorVisible.Should().Be(outcome != "success");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Should_PreserveOtherDraft_WhenTargetChangesDuringFileRead(bool closeTarget)
    {
        await using var host = new TuiFeatureTestHost("en");
        var completion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        A.CallTo(() => host.QueryFiles.ReadAsync("query.js", A<CancellationToken>._)).Returns(completion.Task);
        var target = new MongoConnectionTarget("mongodb://localhost:27017");
        var first = host.Tabs.AddTab(ConnectionId.New(), target, "shop", script: "first draft");
        Task? operation = null;
        Task? background = null;
        var started = false;
        var elapsed = Stopwatch.StartNew();
        host.App.Iteration += (_, _) =>
        {
            if (!started)
            {
                started = true;
                operation = (Task)typeof(MainWindow).GetMethod("OpenQueryFileEntryAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(host.Window, ["query.js"])!;
                background = ChangeTargetAsync();
            }
            if (operation?.IsCompleted == true || elapsed.Elapsed > TimeSpan.FromSeconds(5)) host.App.RequestStop(host.Window);
        };
        async Task ChangeTargetAsync()
        {
            if (closeTarget) await host.Tabs.CloseActiveAsync();
            host.Tabs.AddTab(ConnectionId.New(), target, "other", script: "second draft");
            await Task.Delay(50).ConfigureAwait(false);
            completion.SetResult("loaded script");
        }

        host.App.Run(host.Window);

        operation!.IsCompletedSuccessfully.Should().BeTrue();
        host.Tabs.ActiveTab!.Script.Should().Be("second draft");
        first.Script.Should().Be(closeTarget ? "first draft" : "loaded script");
        if (background is not null) await background;
    }

    private sealed class SaveObservation
    {
        private readonly TuiFeatureTestHost _host;
        private readonly SaveQueryFileDialog _dialog;
        private readonly TaskCompletionSource _completion;
        private readonly string _outcome;
        private readonly Stopwatch _elapsed = Stopwatch.StartNew();
        private bool _started, _completing;
        public bool BusyVisible { get; private set; }
        public bool ErrorVisible { get; private set; }

        public SaveObservation(TuiFeatureTestHost host, SaveQueryFileDialog dialog, TaskCompletionSource completion, string outcome)
        {
            _host = host;
            _dialog = dialog;
            _completion = completion;
            _outcome = outcome;
            host.App.Iteration += (_, _) => Observe();
            host.App.AddTimeout(TimeSpan.FromMilliseconds(20), () =>
            {
                host.App.Run(dialog);
                host.App.RequestStop(host.Window);
                return false;
            });
        }

        private void Observe()
        {
            if (!_dialog.IsRunning) return;
            if (!_started)
            {
                _started = true;
                _dialog.Buttons.Single(button => button.IsDefault).InvokeCommand(Command.Accept);
            }
            var screen = _host.App.Driver!.ToString()!;
            if (!_completing && screen.Contains(_host.Localizer.Get(LocalizationKeys.QueryFiles.Saving)))
            {
                BusyVisible = _dialog.Buttons.All(button => !button.Enabled);
                _completing = true;
                _ = CompleteAsync();
            }
            var message = _outcome == "cancel" ? _host.Localizer.Get(LocalizationKeys.Query.Cancelled) : "disk unavailable";
            if (_outcome != "success" && _dialog.SubViews.OfType<Label>().Last().Text == message && screen.Contains(message))
            {
                ErrorVisible = _dialog.Buttons.All(button => button.Enabled);
                _host.App.RequestStop(_dialog);
            }
            if (_elapsed.Elapsed > TimeSpan.FromSeconds(5))
            {
                // Allow the watchdog to close a dialog even if the busy state never clears.
                typeof(SaveQueryFileDialog).GetField("_saving", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(_dialog, false);
                _host.App.RequestStop(_dialog);
            }
        }

        private async Task CompleteAsync()
        {
            await Task.Delay(50).ConfigureAwait(false);
            if (_outcome == "cancel") _completion.SetCanceled();
            else if (_outcome == "error") _completion.SetException(new IOException("disk unavailable"));
            else _completion.SetResult();
        }
    }
}
