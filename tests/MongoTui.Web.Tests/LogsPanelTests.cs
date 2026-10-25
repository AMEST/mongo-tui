using AwesomeAssertions;
using FakeItEasy;
using Microsoft.Extensions.DependencyInjection;
using MongoTui.Application.Abstractions.Diagnostics;
using MongoTui.Application.Models;
using MongoTui.Web.Components.Panels;
using MongoTui.Web.Localization;

namespace MongoTui.Web.Tests;

public sealed class LogsPanelTests
{
    private const string Latest = "app-20261025.log", Archived = "app-20261024.log";

    [Theory]
    [InlineData("en")]
    [InlineData("ru")]
    public async Task Should_KeepFileSelectionEnabled_WhenLogRefreshIsPending(string language)
    {
        var reader = Reader();
        using var services = Services(reader, language);
        await using var renderer = new ComponentTestRenderer(services);
        await renderer.MountAsync<LogsPanel>(new());
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var response = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        A.CallTo(() => reader.ReadAsync(Latest, A<CancellationToken>._))
            .ReturnsLazily(() => { started.SetResult(); return response.Task; });

        var pending = renderer.ClickAsync("secondary");
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var disabled = await renderer.AttributeAsync("#log-file", "disabled");
        response.SetResult("Refreshed log content");
        await pending;

        disabled.Should().NotBe(true);
        (await renderer.TextAsync()).Should().Contain("Refreshed log content");
    }

    [Fact]
    public async Task Should_KeepNewSelectionAndContent_WhenPreviousRefreshFinishesLate()
    {
        var reader = Reader();
        using var services = Services(reader);
        await using var renderer = new ComponentTestRenderer(services);
        await renderer.MountAsync<LogsPanel>(new());
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var response = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var previousToken = CancellationToken.None;
        A.CallTo(() => reader.ReadAsync(Latest, A<CancellationToken>._)).ReturnsLazily(call =>
        {
            previousToken = call.GetArgument<CancellationToken>(1);
            started.SetResult();
            return response.Task;
        });
        var pending = renderer.ClickAsync("secondary");
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await renderer.ChangeAsync(Archived);
        response.SetResult("Stale current log content");
        await pending;

        previousToken.IsCancellationRequested.Should().BeTrue();
        (await renderer.AttributeAsync("#log-file", "value")).Should().Be(Archived);
        (await renderer.TextAsync()).Should().Contain("Archived log content").And.NotContain("Stale current log content");
    }

    [Fact]
    public async Task Should_CancelPendingRead_WhenPanelIsClosed()
    {
        var reader = Reader();
        using var services = Services(reader);
        await using var renderer = new ComponentTestRenderer(services);
        await renderer.MountAsync<LogsPanel>(new());
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var operationToken = CancellationToken.None;
        A.CallTo(() => reader.ReadAsync(Latest, A<CancellationToken>._)).ReturnsLazily(async call =>
        {
            operationToken = call.GetArgument<CancellationToken>(1);
            started.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, operationToken);
            return string.Empty;
        });
        var pending = renderer.ClickAsync("secondary");
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await renderer.CloseAsync();
        await pending.WaitAsync(TimeSpan.FromSeconds(5));

        operationToken.IsCancellationRequested.Should().BeTrue();
    }

    private static IApplicationLogReader Reader()
    {
        var reader = A.Fake<IApplicationLogReader>();
        IReadOnlyList<ApplicationLogFile> files =
        [new(Latest, 10, DateTimeOffset.UtcNow), new(Archived, 10, DateTimeOffset.UtcNow.AddDays(-1))];
        A.CallTo(() => reader.GetFilesAsync(A<CancellationToken>._)).Returns(files);
        A.CallTo(() => reader.ReadAsync(Latest, A<CancellationToken>._)).Returns("Current log content");
        A.CallTo(() => reader.ReadAsync(Archived, A<CancellationToken>._)).Returns("Archived log content");
        return reader;
    }

    private static ServiceProvider Services(IApplicationLogReader reader, string language = "en")
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(reader);
        var text = new WebText();
        text.SetLanguage(language);
        services.AddSingleton(text);
        return services.BuildServiceProvider();
    }
}
