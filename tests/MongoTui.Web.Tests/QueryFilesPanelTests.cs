using AwesomeAssertions;
using FakeItEasy;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MongoTui.Application.Abstractions.Persistence;
using MongoTui.Application.Localization;
using MongoTui.Application.Models;
using MongoTui.Application.Services;
using MongoTui.Web.Components.Panels;
using MongoTui.Web.Services;

namespace MongoTui.Web.Tests;

public sealed class QueryFilesPanelTests
{
    [Fact]
    public async Task Should_RenderRecentFiles_When_Loaded()
    {
        await using var fixture = new WorkspaceFixture();
        A.CallTo(() => fixture.Repository.GetAllAsync(A<CancellationToken>._)).Returns(new[] { new StoredConnection(fixture.Profile, null) });
        using var services = Services(fixture, "a.js\nb.js");
        await using var renderer = new ComponentTestRenderer(services);

        await renderer.MountAsync<QueryFilesPanel>(new());

        (await renderer.TextAsync()).Should().Contain("a.js").And.Contain("b.js");
    }

    [Fact]
    public async Task Should_RaiseOpenRequest_When_PathSubmitted()
    {
        await using var fixture = new WorkspaceFixture();
        A.CallTo(() => fixture.Repository.GetAllAsync(A<CancellationToken>._)).Returns(new[] { new StoredConnection(fixture.Profile, null) });
        using var services = Services(fixture, null);
        await using var renderer = new ComponentTestRenderer(services);
        QueryFileOpenRequest? opened = null;
        await renderer.MountAsync<QueryFilesPanel>(new()
        {
            [nameof(QueryFilesPanel.OnOpen)] = EventCallback.Factory.Create<QueryFileOpenRequest>(this, request => opened = request),
        });
        await renderer.ChangeAsync("mono", "/tmp/active.js");

        await renderer.SubmitAsync();

        opened.Should().NotBeNull();
        opened!.Path.Should().Be("/tmp/active.js");
        opened.ConnectionId.Should().Be(fixture.Profile.Id);
    }

    [Fact]
    public async Task Should_ShowHint_When_PathBlank()
    {
        await using var fixture = new WorkspaceFixture();
        A.CallTo(() => fixture.Repository.GetAllAsync(A<CancellationToken>._)).Returns(new[] { new StoredConnection(fixture.Profile, null) });
        using var services = Services(fixture, null);
        await using var renderer = new ComponentTestRenderer(services);
        await renderer.MountAsync<QueryFilesPanel>(new());

        await renderer.SubmitAsync();

        (await renderer.TextAsync()).Should().Contain(fixture.Text[LocalizationKeys.QueryFiles.PathRequired]);
    }

    private static ServiceProvider Services(WorkspaceFixture fixture, string? recent)
    {
        var settings = A.Fake<ISettingsStore>();
        A.CallTo(() => settings.GetAsync("query.recentFiles", A<CancellationToken>._)).Returns(recent);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(A.Fake<Microsoft.JSInterop.IJSRuntime>());
        services.AddSingleton(fixture.Catalog);
        services.AddSingleton(fixture.Text);
        services.AddSingleton(new QueryFileService(A.Fake<IQueryFileStore>(), settings, NullLogger<QueryFileService>.Instance));
        return services.BuildServiceProvider();
    }
}
