using System.Text.Encodings.Web;
using AwesomeAssertions;
using FakeItEasy;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using MongoTui.Application.Localization;
using MongoTui.Application.Models;
using MongoTui.Domain.Exceptions;
using MongoTui.Domain.Primitives;
using MongoTui.Domain.Users;
using MongoTui.Web.Components.Panels;
using MongoTui.Web.Services;

namespace MongoTui.Web.Tests;

public sealed class UserManagementDialogTests
{
    [Theory]
    [InlineData("en")]
    [InlineData("ru")]
    public async Task Should_RenderScopedUsersAndProtectCurrentIdentity_WhenOpened(string language)
    {
        await using var fixture = new UserManagementFixture();
        await fixture.ConnectAsync("alice");
        fixture.Workspace.Text.SetLanguage(language);
        A.CallTo(() => fixture.Gateway.ListUsersAsync(A<MongoConnectionTarget>._, DatabaseName.Create("admin"), A<CancellationToken>._))
            .Returns(new MongoUser[] { new("alice", "admin", [new("root", "admin")]), new("reader", "admin", [new("read", "orders")]) });

        var html = await RenderAsync<UserManagementDialog>(fixture, new() { [nameof(UserManagementDialog.Selection)] = fixture.Selection });

        html.Should().Contain(Encode(fixture, LocalizationKeys.Users.Title, "admin"));
        html.Should().Contain("alice").And.Contain("reader").And.Contain("orders");
        html.Should().Contain(Encode(fixture, LocalizationKeys.Users.CurrentUser));
        html.Should().Contain(Encode(fixture, LocalizationKeys.Users.DeleteCurrentForbidden));
        html.Should().Contain("disabled");
        html.Should().NotContain("Users.");
    }

    [Theory]
    [InlineData("en")]
    [InlineData("ru")]
    public async Task Should_RenderRetainedPasswordAndAdditionalRoles_WhenEditing(string language)
    {
        await using var fixture = new UserManagementFixture();
        await fixture.ConnectAsync();
        fixture.Workspace.Text.SetLanguage(language);
        var draft = new UserDraft(new MongoUser("alice", "admin", [new("read", "orders"), new("custom", "reports")]));

        var html = await RenderAsync<UserFormDialog>(fixture, new()
        {
            [nameof(UserFormDialog.Selection)] = fixture.Selection, [nameof(UserFormDialog.Draft)] = draft,
            [nameof(UserFormDialog.KnownDatabases)] = new[] { "orders", "reports" },
        });

        html.Should().Contain("readonly").And.Contain("type=\"password\"");
        html.Should().Contain(Encode(fixture, LocalizationKeys.Users.FormPasswordKeepHint));
        html.Should().Contain(Encode(fixture, LocalizationKeys.Users.AdditionalRolesHint));
        html.Should().Contain("custom").And.Contain("reports");
        html.Should().NotContain("Users.");
    }

    [Fact]
    public async Task Should_DisableOtherManagedPermissions_WhenAdministratorIsSelected()
    {
        await using var fixture = new UserManagementFixture();
        var draft = new UserDraft(new MongoUser("alice", "admin", [new("root", "admin")]));

        var html = await RenderAsync<UserAccessEditor>(fixture, new() { [nameof(UserAccessEditor.Draft)] = draft });

        html.Should().Contain("<fieldset disabled");
        html.Should().Contain(Encode(fixture, LocalizationKeys.Users.ClusterRolesAdministratorWarning));
        html.Should().Contain("checked");
    }

    [Theory]
    [InlineData("en")]
    [InlineData("ru")]
    public async Task Should_RenderOneTimeCredentialsAndSshWarning_WhenPasswordChanges(string language)
    {
        await using var fixture = new UserManagementFixture();
        fixture.Workspace.Text.SetLanguage(language);
        var credentials = new UserCredentials("alice", "new-secret", "mongodb://alice:new-secret@host/?authSource=admin", true);

        var html = await RenderAsync<UserCredentialsDialog>(fixture, new()
        {
            [nameof(UserCredentialsDialog.Credentials)] = credentials,
            [nameof(UserCredentialsDialog.PasswordChanged)] = true,
            [nameof(UserCredentialsDialog.ReconnectRequired)] = true,
        });

        html.Should().Contain("new-secret");
        html.Should().Contain(Encode(fixture, LocalizationKeys.Users.CreatedPasswordShownOnce));
        html.Should().Contain(Encode(fixture, LocalizationKeys.Users.CreatedSshWarning));
        html.Should().Contain(Encode(fixture, LocalizationKeys.Users.PasswordChangedCurrent));
        html.Should().Contain(Encode(fixture, LocalizationKeys.Users.CredentialsCopyPassword));
        html.Should().Contain(Encode(fixture, LocalizationKeys.Users.CreatedCopyConnectionString));
    }

    [Theory]
    [InlineData("en")]
    [InlineData("ru")]
    public async Task Should_ShowLocalizedPermissionError_WhenUserListCannotLoad(string language)
    {
        await using var fixture = new UserManagementFixture();
        await fixture.ConnectAsync();
        fixture.Workspace.Text.SetLanguage(language);
        A.CallTo(() => fixture.Gateway.ListUsersAsync(A<MongoConnectionTarget>._, A<DatabaseName>._, A<CancellationToken>._))
            .ThrowsAsync(new UserManagementException(UserManagementError.InsufficientPrivileges));

        var html = await RenderAsync<UserManagementDialog>(fixture, new() { [nameof(UserManagementDialog.Selection)] = fixture.Selection });

        html.Should().Contain(Encode(fixture, LocalizationKeys.Users.ErrorUnauthorized));
        html.Should().Contain("role=\"alert\"");
        html.Should().NotContain(Encode(fixture, LocalizationKeys.Users.Empty));
    }

    [Theory]
    [InlineData("en")]
    [InlineData("ru")]
    public async Task Should_KeepEmptyStateAndRejectDuplicateRefresh_WhenLoadingAgain(string language)
    {
        await using var fixture = new UserManagementFixture();
        await fixture.ConnectAsync();
        fixture.Workspace.Text.SetLanguage(language);
        using var services = Services(fixture);
        await using var renderer = new ComponentTestRenderer(services);
        await renderer.MountAsync<UserManagementDialog>(new() { [nameof(UserManagementDialog.Selection)] = fixture.Selection });
        var response = new TaskCompletionSource<IReadOnlyList<MongoUser>>(TaskCreationOptions.RunContinuationsAsynchronously);
        A.CallTo(() => fixture.Gateway.ListUsersAsync(A<MongoConnectionTarget>._, A<DatabaseName>._, A<CancellationToken>._))
            .Returns(response.Task);
        Fake.ClearRecordedCalls(fixture.Gateway);

        var pending = renderer.ClickAsync("secondary");
        var during = await renderer.TextAsync();
        await renderer.ClickAsync("secondary");
        response.SetResult([]);
        await pending;

        during.Should().Contain(fixture.Workspace.Text[LocalizationKeys.Users.Empty]);
        A.CallTo(() => fixture.Gateway.ListUsersAsync(A<MongoConnectionTarget>._, A<DatabaseName>._, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    private static ServiceProvider Services(UserManagementFixture fixture)
    {
        var registrations = new ServiceCollection();
        registrations.AddLogging();
        registrations.AddSingleton(fixture.Workspace.Catalog);
        registrations.AddSingleton(fixture.Workspace.Text);
        registrations.AddSingleton(fixture.Users);
        registrations.AddSingleton(A.Fake<IJSRuntime>());
        return registrations.BuildServiceProvider();
    }

    private static string Encode(UserManagementFixture fixture, string key, params object[] args) =>
        HtmlEncoder.Default.Encode(fixture.Workspace.Text.Format(key, args));

    private static async Task<string> RenderAsync<T>(UserManagementFixture fixture, Dictionary<string, object?> parameters) where T : IComponent
    {
        using var services = Services(fixture);
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        var root = await renderer.Dispatcher.InvokeAsync(() => renderer.RenderComponentAsync<T>(ParameterView.FromDictionary(parameters)));
        return await renderer.Dispatcher.InvokeAsync(root.ToHtmlString);
    }
}
