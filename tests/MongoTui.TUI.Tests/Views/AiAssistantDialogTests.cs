using AwesomeAssertions;
using FakeItEasy;
using MongoTui.Application.Abstractions.Ai;
using MongoTui.Domain.Ai;
using MongoTui.Domain.Collections;
using MongoTui.Domain.Exceptions;
using MongoTui.Domain.Primitives;
using MongoTui.TUI.Localization;
using MongoTui.TUI.Views;
using Terminal.Gui.App;
using Terminal.Gui.Input;
using Terminal.Gui.Views;

#pragma warning disable CS0618 // TextView is superseded by the external Editor package.

namespace MongoTui.TUI.Tests.Views;

public sealed class AiAssistantDialogTests
{
    private static readonly QueryAssistantRequest Request = new()
    {
        Schema = new CollectionSchema
        {
            Collection = new CollectionRef(DatabaseName.Create("app"), CollectionName.Create("users")),
            Fields = [],
        },
        Prompt = string.Empty,
    };

    [Fact]
    public async Task Should_RunRequest_When_EnterPressedInPromptField()
    {
        var assistant = CompletedAssistant("draft");
        var (application, poll) = CreateApplication();
        using var dialog = new AiAssistantDialog(assistant, new ResxLocalizer(), application, Request);
        var field = dialog.SubViews.OfType<TextField>().Single();

        field.NewKeyDownEvent(Key.Enter);
        await DriveUntilDoneAsync(poll()!);

        A.CallTo(() => assistant.CompleteAsync(A<QueryAssistantRequest>._, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => application.RequestStop(dialog)).MustNotHaveHappened();
        Output(dialog).Should().Be("draft");
    }

    [Fact]
    public void Should_ShowRunningState_UntilResponseArrives()
    {
        var pending = PendingAssistant();
        var (application, _) = CreateApplication();
        using var dialog = new AiAssistantDialog(pending.Assistant, new ResxLocalizer(), application, Request);

        dialog.AskAsync();

        dialog.IsBusy.Should().BeTrue();
        dialog.StatusText.Should().Be("Requesting…");
        dialog.Buttons[0].Enabled.Should().BeFalse();
    }

    [Fact]
    public async Task Should_ApplyResponseAndStopPolling_When_TaskCompletes()
    {
        var pending = PendingAssistant();
        var (application, poll) = CreateApplication();
        using var dialog = new AiAssistantDialog(pending.Assistant, new ResxLocalizer(), application, Request);

        dialog.AskAsync();
        var notify = poll()!();

        pending.Source.SetResult("draft");
        await DriveUntilDoneAsync(poll()!);

        notify.Should().BeTrue();
        dialog.IsBusy.Should().BeFalse();
        dialog.StatusText.Should().BeEmpty();
        dialog.Buttons[0].Enabled.Should().BeTrue();
        Output(dialog).Should().Be("draft");
    }

    [Fact]
    public async Task Should_ReportError_When_TaskFaults()
    {
        var pending = PendingAssistant();
        var (application, poll) = CreateApplication();
        using var dialog = new AiAssistantDialog(pending.Assistant, new ResxLocalizer(), application, Request);

        dialog.AskAsync();
        pending.Source.SetException(new DomainException("kaboom"));
        await DriveUntilDoneAsync(poll()!);

        dialog.StatusText.Should().Contain("kaboom");
        dialog.IsBusy.Should().BeFalse();
        dialog.Buttons[0].Enabled.Should().BeTrue();
    }

    [Fact]
    public async Task Should_ReportError_When_AssistantThrows()
    {
        var assistant = A.Fake<IAiAssistant>();
        A.CallTo(() => assistant.CompleteAsync(A<QueryAssistantRequest>._, A<CancellationToken>._))
            .Throws(new DomainException("boom"));
        var (application, poll) = CreateApplication();
        using var dialog = new AiAssistantDialog(assistant, new ResxLocalizer(), application, Request);

        dialog.AskAsync();
        await DriveUntilDoneAsync(poll()!);

        dialog.StatusText.Should().Contain("boom");
        dialog.IsBusy.Should().BeFalse();
        dialog.Buttons[0].Enabled.Should().BeTrue();
    }

    private static IAiAssistant CompletedAssistant(string content)
    {
        var assistant = A.Fake<IAiAssistant>();
        A.CallTo(() => assistant.CompleteAsync(A<QueryAssistantRequest>._, A<CancellationToken>._))
            .Returns(Task.FromResult(content));

        return assistant;
    }

    private static (IAiAssistant Assistant, TaskCompletionSource<string> Source) PendingAssistant()
    {
        var source = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var assistant = A.Fake<IAiAssistant>();
        A.CallTo(() => assistant.CompleteAsync(A<QueryAssistantRequest>._, A<CancellationToken>._)).Returns(source.Task);

        return (assistant, source);
    }

    private static (IApplication Application, Func<Func<bool>?> Poll) CreateApplication()
    {
        Func<bool>? captured = null;
        var application = A.Fake<IApplication>();
        A.CallTo(() => application.AddTimeout(A<TimeSpan>._, A<Func<bool>>._))
            .ReturnsLazily((TimeSpan _, Func<bool> callback) =>
            {
                captured = callback;

                return new object();
            });

        return (application, () => captured);
    }

    // Simulates the dialog's main loop firing the poll until it reports completion.
    private static async Task DriveUntilDoneAsync(Func<bool> poll)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline)
        {
            if (!poll())
            {
                return;
            }

            await Task.Delay(10);
        }

        throw new TimeoutException("The poll callback never completed.");
    }

    private static string? Output(AiAssistantDialog dialog) =>
        dialog.SubViews.OfType<TextView>().Single().Text;
}
