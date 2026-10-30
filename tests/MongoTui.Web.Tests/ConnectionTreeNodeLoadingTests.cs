using AwesomeAssertions;
using FakeItEasy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using MongoTui.Domain.Primitives;
using MongoTui.Web.Components.Panels;

namespace MongoTui.Web.Tests;

public sealed class ConnectionTreeNodeLoadingTests
{
    [Theory]
    [InlineData("en")]
    [InlineData("ru")]
    public async Task Should_KeepConnectionHeaderIdle_WhenDatabaseCollectionsAreLoading(string language)
    {
        await using var fixture = new WorkspaceFixture();
        fixture.Text.SetLanguage(language);
        var connection = await fixture.Catalog.ConnectAsync(fixture.Profile, CancellationToken.None);
        fixture.Catalog.ToggleConnection(connection);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var response = new TaskCompletionSource<IReadOnlyList<string>>(TaskCreationOptions.RunContinuationsAsynchronously);
        A.CallTo(() => fixture.Gateway.ListCollectionsAsync(connection.Target, DatabaseName.Create("demo"), A<CancellationToken>._))
            .ReturnsLazily(() => { started.SetResult(); return response.Task; });
        using var services = Services(fixture);
        await using var renderer = new ComponentTestRenderer(services);
        await renderer.MountAsync<ConnectionTreeNode>(new() { [nameof(ConnectionTreeNode.Profile)] = fixture.Profile });

        var pending = renderer.ClickAsync("database");
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var headerDisabled = await renderer.AttributeAsync("connection-toggle", "disabled");
        var headerBusy = await renderer.AttributeAsync("connection-state", "aria-busy");
        var databaseBusy = await renderer.AttributeAsync("database", "aria-busy");
        response.SetResult(["orders"]);
        await pending;

        headerDisabled.Should().NotBe(true);
        headerBusy.Should().Be("false");
        databaseBusy.Should().Be("true");
        connection.Databases.Single().Collections.Should().Equal("orders");
        (await renderer.AttributeAsync("database", "aria-busy")).Should().Be("false");
    }

    [Fact]
    public async Task Should_IgnoreRepeatedExpansion_WhenCollectionsAreLoading()
    {
        await using var fixture = new WorkspaceFixture();
        var connection = await fixture.Catalog.ConnectAsync(fixture.Profile, CancellationToken.None);
        fixture.Catalog.ToggleConnection(connection);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var response = new TaskCompletionSource<IReadOnlyList<string>>(TaskCreationOptions.RunContinuationsAsynchronously);
        A.CallTo(() => fixture.Gateway.ListCollectionsAsync(connection.Target, DatabaseName.Create("demo"), A<CancellationToken>._))
            .ReturnsLazily(() => { started.TrySetResult(); return response.Task; });
        using var services = Services(fixture);
        await using var renderer = new ComponentTestRenderer(services);
        await renderer.MountAsync<ConnectionTreeNode>(new() { [nameof(ConnectionTreeNode.Profile)] = fixture.Profile });
        var pending = renderer.ClickAsync("database");
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await renderer.ClickAsync("database");
        response.SetResult(["orders"]);
        await pending;

        A.CallTo(() => fixture.Gateway.ListCollectionsAsync(connection.Target, DatabaseName.Create("demo"), A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
        connection.Databases.Single().Expanded.Should().BeTrue();
    }

    [Fact]
    public async Task Should_AllowRetry_WhenLoadingCollectionsFailed()
    {
        await using var fixture = new WorkspaceFixture();
        var connection = await fixture.Catalog.ConnectAsync(fixture.Profile, CancellationToken.None);
        fixture.Catalog.ToggleConnection(connection);
        A.CallTo(() => fixture.Gateway.ListCollectionsAsync(connection.Target, DatabaseName.Create("demo"), A<CancellationToken>._))
            .ThrowsAsync(new InvalidOperationException("Could not list collections."));
        using var services = Services(fixture);
        await using var renderer = new ComponentTestRenderer(services);
        await renderer.MountAsync<ConnectionTreeNode>(new() { [nameof(ConnectionTreeNode.Profile)] = fixture.Profile });
        await renderer.ClickAsync("database");
        var databaseBusy = await renderer.AttributeAsync("database", "aria-busy");
        A.CallTo(() => fixture.Gateway.ListCollectionsAsync(connection.Target, DatabaseName.Create("demo"), A<CancellationToken>._))
            .Returns(new[] { "orders" });

        await renderer.ClickAsync("database");

        databaseBusy.Should().Be("false");
        (await renderer.AttributeAsync("connection-toggle", "disabled")).Should().NotBe(true);
        connection.Databases.Single().Expanded.Should().BeTrue();
        connection.Databases.Single().Collections.Should().Equal("orders");
    }

    [Fact]
    public async Task Should_ShowCachedCollectionsImmediately_WhenDatabaseReopened()
    {
        await using var fixture = new WorkspaceFixture();
        var connection = await fixture.Catalog.ConnectAsync(fixture.Profile, CancellationToken.None);
        fixture.Catalog.ToggleConnection(connection);
        A.CallTo(() => fixture.Gateway.ListCollectionsAsync(connection.Target, DatabaseName.Create("demo"), A<CancellationToken>._))
            .Returns(new[] { "orders" });
        await fixture.Catalog.ExpandAsync(connection, connection.Databases.Single(), CancellationToken.None);
        using var services = Services(fixture);
        await using var renderer = new ComponentTestRenderer(services);
        await renderer.MountAsync<ConnectionTreeNode>(new() { [nameof(ConnectionTreeNode.Profile)] = fixture.Profile });
        await renderer.ClickAsync("database");
        var response = new TaskCompletionSource<IReadOnlyList<string>>(TaskCreationOptions.RunContinuationsAsynchronously);
        A.CallTo(() => fixture.Gateway.ListCollectionsAsync(connection.Target, DatabaseName.Create("demo"), A<CancellationToken>._))
            .Returns(response.Task);

        var pending = renderer.ClickAsync("database");
        var text = await renderer.TextAsync();
        var expanded = await renderer.AttributeAsync("database", "aria-expanded");
        response.SetResult(["orders", "new"]);
        await pending;

        expanded.Should().Be("true");
        text.Should().Contain("orders");
        (await renderer.TextAsync()).Should().Contain("new");
    }

    private static ServiceProvider Services(WorkspaceFixture fixture)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(fixture.Catalog);
        services.AddSingleton(fixture.Workspace);
        services.AddSingleton(fixture.Text);
        services.AddSingleton(A.Fake<IJSRuntime>());
        return services.BuildServiceProvider();
    }
}
