using AwesomeAssertions;
using FakeItEasy;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using MongoTui.Application.Localization;
using MongoTui.Application.Models;
using MongoTui.Domain.Connections;
using MongoTui.Web.Components.Panels;
using MongoTui.Web.Localization;
using MongoTui.Web.Services;

namespace MongoTui.Web.Tests;

public sealed class ConnectionFailureLoadingTests
{
    [Theory]
    [InlineData(false, "en")]
    [InlineData(false, "ru")]
    [InlineData(true, "en")]
    [InlineData(true, "ru")]
    public async Task Should_StopSpinnerAndAllowRetry_WhenParentDisplaysConnectionFailure(bool list, string language)
    {
        await using var fixture = new WorkspaceFixture();
        fixture.Text.SetLanguage(language);
        A.CallTo(() => fixture.Repository.GetAllAsync(A<CancellationToken>._)).Returns(new[] { new StoredConnection(fixture.Profile, null) });
        var response = new TaskCompletionSource<IReadOnlyList<DatabaseInfo>>(TaskCreationOptions.RunContinuationsAsynchronously);
        A.CallTo(() => fixture.Gateway.ListDatabasesAsync(A<MongoConnectionTarget>._, A<CancellationToken>._)).Returns(response.Task);
        var registrations = new ServiceCollection();
        registrations.AddLogging();
        registrations.AddSingleton(fixture.Catalog);
        registrations.AddSingleton(fixture.Workspace);
        registrations.AddSingleton(fixture.Text);
        registrations.AddSingleton(A.Fake<IJSRuntime>());
        using var services = registrations.BuildServiceProvider();
        await using var renderer = new ComponentTestRenderer(services);
        await renderer.MountAsync<FailureHost>(new() { [nameof(FailureHost.Profile)] = fixture.Profile, [nameof(FailureHost.List)] = list });
        var selector = list ? "secondary" : "connection-toggle";

        var pending = renderer.ClickAsync(selector);
        (await renderer.AttributeAsync(selector, "aria-busy")).Should().Be("true");
        response.SetException(new TimeoutException("Database is unavailable."));
        await pending;

        (await renderer.TextAsync()).Should().Contain(fixture.Text[LocalizationKeys.Error.Connection]);
        (await renderer.AttributeAsync(selector, "aria-busy")).Should().Be("false");
        (await renderer.AttributeAsync("busy-icon", "data-busy")).Should().Be("false");
        A.CallTo(() => fixture.Gateway.ListDatabasesAsync(A<MongoConnectionTarget>._, A<CancellationToken>._))
            .Returns(new[] { new DatabaseInfo("demo", false) });
        await renderer.ClickAsync(selector);
        fixture.Catalog.Active.Should().ContainSingle();
    }

    private sealed class FailureHost : ComponentBase
    {
        [Inject] public ConnectionCatalog Catalog { get; set; } = null!;
        [Inject] public WebText Text { get; set; } = null!;
        [Parameter] public ConnectionProfile Profile { get; set; } = null!;
        [Parameter] public bool List { get; set; }
        private string? _error;

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.AddContent(0, _error);
            builder.OpenComponent(1, List ? typeof(ConnectionList) : typeof(ConnectionTreeNode));
            if (!List) builder.AddAttribute(2, nameof(ConnectionTreeNode.Profile), Profile);
            builder.AddAttribute(3, nameof(ConnectionTreeNode.OnConnect), EventCallback.Factory.Create<ConnectionProfile>(this, ConnectAsync));
            builder.CloseComponent();
        }

        private async Task ConnectAsync(ConnectionProfile profile)
        {
            try { await Catalog.ConnectAsync(profile, CancellationToken.None); }
            catch { _error = Text[LocalizationKeys.Error.Connection]; }
        }
    }
}
