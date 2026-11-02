using AwesomeAssertions;
using FakeItEasy;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MongoTui.Application.Abstractions.Persistence;
using MongoTui.Application.Localization;
using MongoTui.Application.Services;
using MongoTui.Web.Components.Panels;
using MongoTui.Web.Services;

namespace MongoTui.Web.Tests;

public sealed class SaveQueryFileDialogTests
{
    [Theory]
    [InlineData("en")]
    [InlineData("ru")]
    public async Task Should_SaveAndRememberPath_WhenPathProvided(string language)
    {
        var fixture = new WorkspaceFixture();
        fixture.Text.SetLanguage(language);
        var store = A.Fake<IQueryFileStore>();
        var tab = new WebTab(fixture.Profile.Id, "demo", "orders") { Script = "db.orders.find({})" };
        using var services = CreateServices(fixture, store);
        await using var renderer = new ComponentTestRenderer(services);
        var saved = false;
        await renderer.MountAsync<SaveQueryFileDialog>(new()
        {
            [nameof(SaveQueryFileDialog.Tab)] = tab,
            [nameof(SaveQueryFileDialog.OnSaved)] = EventCallback.Factory.Create(this, () => saved = true),
        });
        await renderer.ChangeAsync("#query-file-path", "/tmp/orders.js");

        await renderer.SubmitAsync();

        saved.Should().BeTrue();
        tab.QueryFilePath.Should().Be("/tmp/orders.js");
        A.CallTo(() => store.WriteAsync("/tmp/orders.js", "db.orders.find({})", A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        await fixture.DisposeAsync();
    }

    [Fact]
    public async Task Should_ShowHintAndNotSave_WhenPathBlank()
    {
        var fixture = new WorkspaceFixture();
        var store = A.Fake<IQueryFileStore>();
        var tab = new WebTab(fixture.Profile.Id, "demo", "orders") { Script = "db.orders.find({})" };
        using var services = CreateServices(fixture, store);
        await using var renderer = new ComponentTestRenderer(services);
        await renderer.MountAsync<SaveQueryFileDialog>(new() { [nameof(SaveQueryFileDialog.Tab)] = tab });

        await renderer.SubmitAsync();

        (await renderer.TextAsync()).Should().Contain(fixture.Text[LocalizationKeys.QueryFiles.PathRequired]);
        A.CallTo(() => store.WriteAsync(A<string>._, A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
        await fixture.DisposeAsync();
    }

    [Fact]
    public async Task Should_ShowError_WhenSaveFails()
    {
        var fixture = new WorkspaceFixture();
        var store = A.Fake<IQueryFileStore>();
        A.CallTo(() => store.WriteAsync(A<string>._, A<string>._, A<CancellationToken>._)).ThrowsAsync(new IOException("Access denied"));
        var tab = new WebTab(fixture.Profile.Id, "demo", "orders") { Script = "db.orders.find({})" };
        using var services = CreateServices(fixture, store);
        await using var renderer = new ComponentTestRenderer(services);
        await renderer.MountAsync<SaveQueryFileDialog>(new() { [nameof(SaveQueryFileDialog.Tab)] = tab });
        await renderer.ChangeAsync("#query-file-path", "/tmp/orders.js");

        await renderer.SubmitAsync();

        (await renderer.TextAsync()).Should().Contain(fixture.Text[LocalizationKeys.QueryFiles.SaveError]);
        tab.QueryFilePath.Should().BeNull();
        await fixture.DisposeAsync();
    }

    private static ServiceProvider CreateServices(WorkspaceFixture fixture, IQueryFileStore store)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(A.Fake<Microsoft.JSInterop.IJSRuntime>());
        services.AddSingleton(fixture.Text);
        services.AddSingleton(new QueryFileService(store, A.Fake<ISettingsStore>(), NullLogger<QueryFileService>.Instance));
        return services.BuildServiceProvider();
    }
}
