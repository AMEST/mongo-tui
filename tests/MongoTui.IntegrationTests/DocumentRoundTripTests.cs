using Microsoft.Extensions.Logging.Abstractions;
using AwesomeAssertions;
using MongoDB.Bson;
using MongoTui.Application.Services;
using MongoTui.Testing;
using MongoTui.TUI.ViewModels;
using MongoTui.Web.Tests;

namespace MongoTui.IntegrationTests;

[Trait("Category", "Integration")]
public sealed class DocumentRoundTripTests(MongoFixture fixture) : IClassFixture<MongoFixture>
{
    [Theory]
    [InlineData("service", false)]
    [InlineData("service", true)]
    [InlineData("tui-driver", false)]
    [InlineData("tui-driver", true)]
    [InlineData("tui-shell", false)]
    [InlineData("tui-shell", true)]
    [InlineData("tui-filter", false)]
    [InlineData("tui-filter", true)]
    [InlineData("web-driver", false)]
    [InlineData("web-driver", true)]
    [InlineData("web-shell", false)]
    [InlineData("web-shell", true)]
    public async Task Should_PreserveAllOtherBsonBytes_When_DocumentSaved(string path, bool edit)
    {
        using var database = new DocumentTestDatabase(fixture);
        var expected = BsonDocumentSamples.RichDocument();
        await database.InsertAsync(expected);
        var selected = path.EndsWith("shell", StringComparison.Ordinal)
            ? await database.ReadShellAsync() : await database.ReadAsync();

        if (path.StartsWith("web", StringComparison.Ordinal)) await SaveWebAsync(database, selected, edit);
        else if (path.StartsWith("tui", StringComparison.Ordinal)) await SaveTuiAsync(database, selected, path, edit);
        else await SaveServiceAsync(database, selected, edit);

        if (edit) expected["edit"] = "after";
        (await database.ReadBytesAsync()).Should().Equal(expected.ToBson());
    }

    [Fact]
    public async Task Should_SaveCanonicalText_When_RichDocumentInsertedThroughService()
    {
        using var database = new DocumentTestDatabase(fixture);
        var expected = BsonDocumentSamples.RichDocument();
        var documents = new DocumentService(database.Gateway, NullLogger<DocumentService>.Instance);

        await documents.InsertAsync(database.Target, database.Collection, BsonTextCodec.Format(expected), CancellationToken.None);

        (await database.ReadBytesAsync()).Should().Equal(expected.ToBson());
    }

    private static async Task SaveServiceAsync(DocumentTestDatabase database, BsonDocument selected, bool edit)
    {
        var documents = new DocumentService(database.Gateway, NullLogger<DocumentService>.Instance);
        var content = await documents.LoadForEditAsync(database.Target, database.Collection, selected, CancellationToken.None);
        content.Should().NotBeNull();

        var matched = await documents.ReplaceAsync(database.Target, database.Collection, content!.IdFilter,
            Edit(content.Json, edit), CancellationToken.None);

        matched.Should().Be(1);
    }

    private static async Task SaveTuiAsync(DocumentTestDatabase database, BsonDocument selected, string path, bool edit)
    {
        var documents = new DocumentService(database.Gateway, NullLogger<DocumentService>.Instance);
        var model = path == "tui-filter" ? new DocumentEditViewModel(documents, database.Target, database.Collection)
            { Filter = BsonTextCodec.Format(BsonTextCodec.IdFilter(selected)) }
            : new DocumentEditViewModel(documents, database.Target, database.Collection, selected);
        var loaded = path == "tui-filter" ? await model.LoadAsync(CancellationToken.None)
            : await model.InitializeAsync(CancellationToken.None);
        loaded.Should().BeTrue();
        model.Json = Edit(model.Json, edit);

        var saved = await model.SaveAsync(CancellationToken.None);

        saved.Should().BeTrue(model.ValidationError ?? "the document must be writable");
    }

    private static async Task SaveWebAsync(DocumentTestDatabase database, BsonDocument selected, bool edit)
    {
        await using var dialog = new DocumentDialogFixture(database.Gateway, database.Target.ConnectionString);
        await dialog.OpenAsync(selected, database.Collection);
        if (edit) await dialog.EditAsync(Edit(await dialog.EditorTextAsync(), true));

        await dialog.SaveAsync();

        dialog.Saved.Should().BeTrue(await dialog.Renderer.TextAsync());
    }

    private static string Edit(string json, bool edit) => edit
        ? json.Replace("\"before\"", "\"after\"", StringComparison.Ordinal) : json;
}
