using Microsoft.Extensions.Logging.Abstractions;
using FakeItEasy;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using MongoDB.Bson;
using MongoTui.Application.Abstractions.Mongo;
using MongoTui.Application.Services;
using MongoTui.Domain.Collections;
using MongoTui.Web.Components.Panels;
using MongoTui.Web.Services;

namespace MongoTui.Web.Tests;

/// <summary>Runs the actual dialog, including its editor events, against a supplied gateway.</summary>
internal sealed class DocumentDialogFixture : IAsyncDisposable
{
    public WorkspaceFixture Workspace { get; }
    public ComponentTestRenderer Renderer { get; }
    public bool Saved { get; private set; }
    private readonly ServiceProvider _services;

    public DocumentDialogFixture(IMongoGateway gateway, string connectionString = "mongodb://localhost:27017")
    {
        Workspace = new WorkspaceFixture(connectionString: connectionString);
        var js = A.Fake<IJSRuntime>();
        A.CallTo(() => js.InvokeAsync<IJSObjectReference>("import", A<object?[]>._))
            .Throws(new JSException("Use the editor fallback in component tests."));
        var registrations = new ServiceCollection();
        registrations.AddLogging();
        registrations.AddSingleton(js);
        registrations.AddSingleton(Workspace.Catalog);
        registrations.AddSingleton(Workspace.Text);
        registrations.AddSingleton(new DocumentService(gateway, NullLogger<DocumentService>.Instance));
        _services = registrations.BuildServiceProvider();
        Renderer = new ComponentTestRenderer(_services);
    }

    public async Task OpenAsync(BsonDocument selected, CollectionRef collection, bool editable = true)
    {
        await Workspace.Catalog.ConnectAsync(Workspace.Profile, CancellationToken.None);
        var tab = new WebTab(Workspace.Profile.Id, collection.Database.Value, collection.Collection.Value)
        {
            EditableCollection = editable ? collection : null,
        };
        await Renderer.MountAsync<DocumentDialog>(new()
        {
            [nameof(DocumentDialog.Tab)] = tab,
            [nameof(DocumentDialog.Document)] = selected,
            [nameof(DocumentDialog.OnSaved)] = EventCallback.Factory.Create(this, () => Saved = true),
        });
    }

    public Task EditAsync(string json) => Renderer.ChangeAsync(json);
    public async Task<string> EditorTextAsync() => (string)(await Renderer.AttributeAsync("editor-fallback", "value"))!;
    public Task SaveAsync() => Renderer.ClickAsync("primary");

    public async ValueTask DisposeAsync()
    {
        await Renderer.CloseAsync();
        await _services.DisposeAsync();
        await Workspace.DisposeAsync();
    }
}
