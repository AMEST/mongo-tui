using Microsoft.JSInterop;
using AwesomeAssertions;
using FakeItEasy;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MongoTui.Application.Models;
using MongoTui.Domain.Primitives;
using MongoTui.Domain.Users;
using MongoTui.Web.Components.Panels;
using MongoTui.Web.Services;

namespace MongoTui.Web.Tests;

public sealed class UserFormLifecycleTests
{
    [Fact]
    public async Task Should_CreateUserOnlyOnce_WhenSubmittedAgainWhileSaveIsPending()
    {
        await using var fixture = new UserManagementFixture();
        await fixture.ConnectAsync();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var response = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        A.CallTo(() => fixture.Gateway.CreateUserAsync(A<MongoConnectionTarget>._, A<DatabaseName>._, A<string>._, A<string>._,
            A<IReadOnlyList<MongoRole>>._, A<CancellationToken>._)).ReturnsLazily(() => { started.SetResult(); return response.Task; });
        var saved = new List<UserSaveResult>();
        var draft = Draft();
        using var services = Services(fixture);
        await using var renderer = new ComponentTestRenderer(services);
        await renderer.MountAsync<UserFormDialog>(new()
        {
            [nameof(UserFormDialog.Draft)] = draft, [nameof(UserFormDialog.Selection)] = fixture.Selection,
            [nameof(UserFormDialog.OnSaved)] = EventCallback.Factory.Create<UserSaveResult>(saved, saved.Add),
        });
        var pending = renderer.SubmitAsync();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await renderer.SubmitAsync();
        response.SetResult();
        await pending;

        saved.Should().ContainSingle().Which.Credentials!.Password.Should().Be("secret");
        draft.Password.Should().BeEmpty();
        A.CallTo(() => fixture.Gateway.CreateUserAsync(A<MongoConnectionTarget>._, A<DatabaseName>._, A<string>._, A<string>._,
            A<IReadOnlyList<MongoRole>>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Should_CancelPendingSaveAndClearPassword_WhenDialogIsDisposed()
    {
        await using var fixture = new UserManagementFixture();
        await fixture.ConnectAsync();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var operationToken = CancellationToken.None;
        A.CallTo(() => fixture.Gateway.CreateUserAsync(A<MongoConnectionTarget>._, A<DatabaseName>._, A<string>._, A<string>._,
            A<IReadOnlyList<MongoRole>>._, A<CancellationToken>._)).ReturnsLazily(call =>
            {
                operationToken = call.GetArgument<CancellationToken>(5);
                started.SetResult();
                return Task.Delay(Timeout.InfiniteTimeSpan, operationToken);
            });
        var saved = new List<UserSaveResult>();
        var draft = Draft();
        using var services = Services(fixture);
        await using var renderer = new ComponentTestRenderer(services);
        await renderer.MountAsync<UserFormDialog>(new()
        {
            [nameof(UserFormDialog.Draft)] = draft, [nameof(UserFormDialog.Selection)] = fixture.Selection,
            [nameof(UserFormDialog.OnSaved)] = EventCallback.Factory.Create<UserSaveResult>(saved, saved.Add),
        });
        var pending = renderer.SubmitAsync();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await renderer.CloseAsync();
        await pending.WaitAsync(TimeSpan.FromSeconds(5));

        operationToken.IsCancellationRequested.Should().BeTrue();
        saved.Should().BeEmpty();
        draft.Password.Should().BeEmpty();
    }

    private static UserDraft Draft()
    {
        var draft = new UserDraft { Username = "reader", Password = "secret" };
        draft.SetDatabaseAccess("orders", DatabaseAccessLevel.Read);
        return draft;
    }

    private static ServiceProvider Services(UserManagementFixture fixture)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(A.Fake<IJSRuntime>());
        services.AddSingleton(fixture.Users);
        services.AddSingleton(fixture.Workspace.Text);
        return services.BuildServiceProvider();
    }
}
