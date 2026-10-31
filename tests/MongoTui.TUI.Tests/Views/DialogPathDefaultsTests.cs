using System.Text;
using AwesomeAssertions;
using MongoTui.Domain.Collections;
using MongoTui.Domain.Primitives;
using MongoTui.Domain.Transfer;
using MongoTui.TUI.Views;

namespace MongoTui.TUI.Tests.Views;

public sealed class DialogPathDefaultsTests
{
    [Theory]
    [InlineData(ExportFormat.Json, ".jsonl")]
    [InlineData(ExportFormat.CanonicalJson, ".jsonl")]
    [InlineData(ExportFormat.Csv, ".csv")]
    [InlineData(ExportFormat.Bson, ".bson")]
    public void Should_GenerateCollectionFilenameWithTimestamp_WhenSavePathSuggested(ExportFormat format, string extension)
    {
        var home = Path.Combine("home", "user");
        var collection = new CollectionRef(DatabaseName.Create("app"), CollectionName.Create("items"));
        var timestamp = new DateTimeOffset(2026, 11, 1, 9, 8, 7, 6, TimeSpan.Zero);

        var path = DialogPathDefaults.ExportPath(home, collection, timestamp, format);

        path.Should().Be(Path.Combine(home, "app.items_20261101_090807006" + extension));
    }

    [Theory]
    [InlineData("../orders\\file:name?*|\"<>")]
    [InlineData("orders\n\twith-controls")]
    public void Should_KeepFilenameInsideHomeAndPortable_WhenCollectionContainsPathCharacters(string name)
    {
        var home = Path.Combine("home", "user");
        var collection = new CollectionRef(DatabaseName.Create("app"), CollectionName.Create(name));

        var path = DialogPathDefaults.ExportPath(home, collection, DateTimeOffset.UnixEpoch, ExportFormat.CanonicalJson);

        Path.GetDirectoryName(path).Should().Be(home);
        Path.GetFileName(path).Should().NotContainAny("<", ">", ":", "\"", "/", "\\", "|", "?", "*", "\n", "\t");
    }

    [Fact]
    public void Should_BoundFilenameLengthWithoutBreakingUnicode_WhenCollectionNameIsLong()
    {
        var collection = new CollectionRef(DatabaseName.Create("app"), CollectionName.Create(new string('界', 80)));

        var path = DialogPathDefaults.ExportPath("home", collection, DateTimeOffset.UnixEpoch, ExportFormat.Json);

        Encoding.UTF8.GetByteCount(Path.GetFileName(path)).Should().BeLessThan(255);
        Path.GetFileName(path).Should().Contain("界").And.NotContain("�");
    }
}
