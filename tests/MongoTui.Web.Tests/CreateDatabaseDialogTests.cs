using Microsoft.JSInterop;
using FakeItEasy;
using System.Text.Encodings.Web;
using AwesomeAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MongoTui.Application.Services;
using MongoTui.Web.Components.Panels;

namespace MongoTui.Web.Tests;

public sealed class CreateDatabaseDialogTests
{
    [Theory]
    [InlineData("en", "Create database", "Database name", "First collection")]
    [InlineData("ru", "Создание базы данных", "Название базы", "Первая коллекция")]
    public async Task Should_RenderLocalizedDatabaseAndCollectionFields_WhenOpened(string language, string title, string database, string collection)
    {
        await using var fixture = new WorkspaceFixture();
        fixture.Text.SetLanguage(language);
        var registrations = new ServiceCollection();
        registrations.AddLogging();
        registrations.AddSingleton(A.Fake<IJSRuntime>());
        registrations.AddSingleton(fixture.Catalog);
        registrations.AddSingleton(fixture.Text);
        registrations.AddSingleton(new DatabaseService(fixture.Gateway));
        using var services = registrations.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());

        var root = await renderer.Dispatcher.InvokeAsync(() => renderer.RenderComponentAsync<CreateDatabaseDialog>(
            ParameterView.FromDictionary(new Dictionary<string, object?> { [nameof(CreateDatabaseDialog.Profile)] = fixture.Profile })));

        var html = await renderer.Dispatcher.InvokeAsync(root.ToHtmlString);
        html.Should().Contain(HtmlEncoder.Default.Encode(title));
        html.Should().Contain(HtmlEncoder.Default.Encode(database));
        html.Should().Contain(HtmlEncoder.Default.Encode(collection));
        html.Should().Contain("required");
    }
}
