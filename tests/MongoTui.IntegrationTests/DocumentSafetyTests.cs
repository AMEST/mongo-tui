using Microsoft.Extensions.Logging.Abstractions;
using AwesomeAssertions;
using MongoDB.Bson;
using MongoTui.Application.Localization;
using MongoTui.Application.Models;
using MongoTui.Application.Services;
using MongoTui.TUI.ViewModels;
using MongoTui.Web.Tests;

namespace MongoTui.IntegrationTests;

[Trait("Category", "Integration")]
public sealed class DocumentSafetyTests(MongoFixture fixture) : IClassFixture<MongoFixture>
{
    [Theory]
    [InlineData("service", "$date", "2020-01-01T00:00:00Z")]
    [InlineData("service", "$numberLong", "42")]
    [InlineData("tui", "$date", "2020-01-01T00:00:00Z")]
    [InlineData("tui", "$numberLong", "42")]
    [InlineData("web", "$date", "2020-01-01T00:00:00Z")]
    [InlineData("web", "$numberLong", "42")]
    public async Task Should_LeaveDatabaseUntouched_When_JsonCannotRepresentLiteralDocument(string path, string key, string value)
    {
        using var database = new DocumentTestDatabase(fixture);
        var original = new BsonDocument { { "_id", 1 }, { "edit", "before" },
            { "nested", new BsonArray { new BsonDocument(key, value) } } };
        await database.InsertAsync(original);
        var selected = await database.ReadAsync();

        if (path == "web") await RefuseWebAsync(database, selected);
        else if (path == "tui") await RefuseTuiAsync(database, selected);
        else
        {
            var service = new DocumentService(database.Gateway, NullLogger<DocumentService>.Instance);
            var act = () => service.LoadForEditAsync(database.Target, database.Collection, selected, CancellationToken.None);
            (await act.Should().ThrowAsync<DocumentEditException>()).Which.ErrorKey.Should().Be(LocalizationKeys.Documents.UnsafeJson);
        }

        (await database.ReadBytesAsync()).Should().Equal(original.ToBson());
    }

    [Fact]
    public async Task Should_ExposeKnownNormalization_When_DriverReadsNegativeZero()
    {
        using var database = new DocumentTestDatabase(fixture);
        var original = new BsonDocument { { "_id", 1 }, { "zero", new BsonDouble(-0.0) } };
        await database.InsertAsync(original);

        var decoded = await database.ReadAsync();

        (await database.ReadBytesAsync()).Should().Equal(original.ToBson());
        BitConverter.DoubleToInt64Bits(decoded["zero"].AsDouble).Should().Be(0);
        decoded.ToBson().Should().NotEqual(original.ToBson());
    }

    private static async Task RefuseTuiAsync(DocumentTestDatabase database, BsonDocument selected)
    {
        var model = new DocumentEditViewModel(new DocumentService(database.Gateway, NullLogger<DocumentService>.Instance), database.Target, database.Collection, selected);

        (await model.InitializeAsync(CancellationToken.None)).Should().BeFalse();
        (await model.SaveAsync(CancellationToken.None)).Should().BeFalse();

        model.CanWrite.Should().BeFalse();
        model.ValidationErrorKey.Should().Be(LocalizationKeys.Documents.UnsafeJson);
    }

    private static async Task RefuseWebAsync(DocumentTestDatabase database, BsonDocument selected)
    {
        await using var dialog = new DocumentDialogFixture(database.Gateway, database.Target.ConnectionString);

        await dialog.OpenAsync(selected, database.Collection);
        await dialog.SaveAsync();

        dialog.Saved.Should().BeFalse();
        (await dialog.Renderer.TextAsync()).Should().Contain(dialog.Workspace.Text[LocalizationKeys.Documents.UnsafeJson]);
    }
}
