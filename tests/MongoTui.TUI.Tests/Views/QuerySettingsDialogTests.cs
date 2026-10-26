using AwesomeAssertions;
using FakeItEasy;
using MongoTui.Application.Abstractions.Persistence;
using MongoTui.Application.Services;
using MongoTui.TUI.Localization;
using MongoTui.TUI.Views;
using Terminal.Gui.App;
using Terminal.Gui.Views;

namespace MongoTui.TUI.Tests.Views;

public sealed class QuerySettingsDialogTests
{
    [Theory]
    [InlineData("0")]
    [InlineData("120")]
    [InlineData("86400")]
    public async Task Should_PersistSharedTimeout_WhenSaved(string value)
    {
        var store = A.Fake<ISettingsStore>();
        var application = A.Fake<IApplication>();
        A.CallTo(() => application.MainThreadId).Returns(Environment.CurrentManagedThreadId);
        using var dialog = new QuerySettingsDialog(new SettingsService(store), new ResxLocalizer(), application);
        await dialog.LoadAsync(CancellationToken.None);
        dialog.SubViews.OfType<TextField>().Single().Text = value;

        await dialog.SaveAsync();

        A.CallTo(() => store.SetAsync("query.timeoutSeconds", value, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        dialog.Saved.Should().BeTrue();
        A.CallTo(() => application.RequestStop(dialog)).MustHaveHappenedOnceExactly();
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("invalid")]
    [InlineData("86401")]
    public async Task Should_KeepDialogOpen_WhenTimeoutInvalid(string value)
    {
        var store = A.Fake<ISettingsStore>();
        var application = A.Fake<IApplication>();
        A.CallTo(() => application.MainThreadId).Returns(Environment.CurrentManagedThreadId);
        using var dialog = new QuerySettingsDialog(new SettingsService(store), new ResxLocalizer(), application);
        await dialog.LoadAsync(CancellationToken.None);
        dialog.SubViews.OfType<TextField>().Single().Text = value;

        await dialog.SaveAsync();

        A.CallTo(() => store.SetAsync(A<string>._, A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
        dialog.Saved.Should().BeFalse();
        A.CallTo(() => application.RequestStop(dialog)).MustNotHaveHappened();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Should_CancelAndIgnoreResult_WhenDisposedDuringOperation(bool load)
    {
        var store = A.Fake<ISettingsStore>();
        var application = A.Fake<IApplication>();
        A.CallTo(() => application.MainThreadId).Returns(Environment.CurrentManagedThreadId);
        var response = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var requestToken = CancellationToken.None;
        var dialog = new QuerySettingsDialog(new SettingsService(store), new ResxLocalizer(), application);
        Task operation;
        using (dialog)
        {
            await dialog.LoadAsync(CancellationToken.None);
            if (load) A.CallTo(() => store.GetAsync("query.timeoutSeconds", A<CancellationToken>._))
                .ReturnsLazily((string _, CancellationToken token) => { requestToken = token; return response.Task; });
            else A.CallTo(() => store.SetAsync("query.timeoutSeconds", A<string>._, A<CancellationToken>._))
                .ReturnsLazily((string _, string _, CancellationToken token) => { requestToken = token; return response.Task; });
            operation = load ? dialog.LoadAsync(CancellationToken.None) : dialog.SaveAsync();
        }

        response.SetResult("120");
        await operation.WaitAsync(TimeSpan.FromSeconds(2));

        requestToken.IsCancellationRequested.Should().BeTrue();
        dialog.Saved.Should().BeFalse();
        A.CallTo(() => application.RequestStop(dialog)).MustNotHaveHappened();
    }
}
