using System.Text;
using AwesomeAssertions;
using MongoTui.Infrastructure.FileSystem;

namespace MongoTui.Infrastructure.Tests.FileSystem;

public sealed class FileApplicationLogReaderTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"mongotui-logs-{Guid.NewGuid():N}");
    private readonly FileApplicationLogReader _reader;

    public FileApplicationLogReaderTests()
    {
        Directory.CreateDirectory(_directory);
        _reader = new FileApplicationLogReader(_directory);
    }

    [Fact]
    public async Task Should_ListRetainedLogsNewestFirst_WhenDirectoryContainsOtherFiles()
    {
        await File.WriteAllTextAsync(Path.Combine(_directory, "app-20261023.log"), "old");
        await File.WriteAllTextAsync(Path.Combine(_directory, "app-20261024.log"), "new");
        await File.WriteAllTextAsync(Path.Combine(_directory, "other.log"), "unrelated");
        await File.WriteAllTextAsync(Path.Combine(_directory, "kek.key"), "unrelated");
        var nested = Directory.CreateDirectory(Path.Combine(_directory, "nested"));
        await File.WriteAllTextAsync(Path.Combine(nested.FullName, "app-20261025.log"), "nested");

        var files = await _reader.GetFilesAsync(CancellationToken.None);

        files.Select(file => file.Name).Should().Equal("app-20261024.log", "app-20261023.log");
        files[0].Length.Should().Be(3);
        files[0].LastWriteTimeUtc.Offset.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public async Task Should_ReadCompleteUtf8Text_WhenLogContainsLongMultilineMessages()
    {
        var text = "2026-10-24 [ERR] Ошибка подключения\n    <details>\n" + new string('я', 100_000) + "\nlast line\n";
        await File.WriteAllTextAsync(Path.Combine(_directory, "app-20261024.log"), text, Encoding.UTF8);

        var content = await _reader.ReadAsync("app-20261024.log", CancellationToken.None);

        content.Should().Be(text);
    }

    [Fact]
    public async Task Should_ReadAppendedEntries_WhenWriterKeepsLogOpen()
    {
        var path = Path.Combine(_directory, "app-20261024.log");
        await using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
        await using var writer = new StreamWriter(stream);
        await writer.WriteLineAsync("first");
        await writer.FlushAsync();
        var initial = await _reader.GetFilesAsync(CancellationToken.None);
        (await _reader.ReadAsync("app-20261024.log", CancellationToken.None)).Should().Be("first" + Environment.NewLine);
        await writer.WriteLineAsync("second");
        await writer.FlushAsync();

        var content = await _reader.ReadAsync("app-20261024.log", CancellationToken.None);
        var updated = await _reader.GetFilesAsync(CancellationToken.None);

        content.Should().Be("first" + Environment.NewLine + "second" + Environment.NewLine);
        updated[0].Length.Should().BeGreaterThan(initial[0].Length);
    }

    [Fact]
    public async Task Should_ReturnEmpty_WhenLogDirectoryDoesNotExist()
    {
        Directory.Delete(_directory);

        var files = await _reader.GetFilesAsync(CancellationToken.None);
        var content = await _reader.ReadAsync("app-20261024.log", CancellationToken.None);

        files.Should().BeEmpty();
        content.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_ReturnEmpty_WhenSelectedLogWasRemovedByRotation()
    {
        var path = Path.Combine(_directory, "app-20261024.log");
        await File.WriteAllTextAsync(path, "old");
        File.Delete(path);

        var content = await _reader.ReadAsync("app-20261024.log", CancellationToken.None);

        content.Should().BeEmpty();
    }

    [Theory]
    [InlineData("../app-20261024.log")]
    [InlineData("..\\app-20261024.log")]
    [InlineData("/tmp/app-20261024.log")]
    [InlineData("kek.key")]
    [InlineData("app-20261024.txt")]
    [InlineData("")]
    public async Task Should_RejectFileName_WhenItDoesNotIdentifyAnApplicationLog(string fileName)
    {
        var act = () => _reader.ReadAsync(fileName, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Should_CancelReadOrListing_WhenCallerHasCancelled(bool listing)
    {
        var token = new CancellationToken(canceled: true);

        var act = async () =>
        {
            if (listing) await _reader.GetFilesAsync(token);
            else await _reader.ReadAsync("app-20261024.log", token);
        };

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Should_ExcludeAndRejectSymbolicLinks_WhenLogNamePointsToAnotherFile()
    {
        if (OperatingSystem.IsWindows()) return;
        var target = Path.Combine(_directory, "private.txt");
        await File.WriteAllTextAsync(target, "not a log");
        File.CreateSymbolicLink(Path.Combine(_directory, "app-20261024.log"), target);

        var files = await _reader.GetFilesAsync(CancellationToken.None);
        var act = () => _reader.ReadAsync("app-20261024.log", CancellationToken.None);

        files.Should().BeEmpty();
        await act.Should().ThrowAsync<ArgumentException>();
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }
}
