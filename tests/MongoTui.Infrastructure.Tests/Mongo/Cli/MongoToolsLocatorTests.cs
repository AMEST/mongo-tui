using AwesomeAssertions;
using MongoTui.Infrastructure.Mongo.Cli;
using MongoTui.Infrastructure.Options;

namespace MongoTui.Infrastructure.Tests.Mongo.Cli;

public sealed class MongoToolsLocatorTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"mongotools-{Guid.NewGuid():N}");

    public MongoToolsLocatorTests() => Directory.CreateDirectory(_directory);

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public void Should_ResolveFromConfiguredDirectory_When_ExecutableExists()
    {
        var executable = OperatingSystem.IsWindows() ? "mongosh.exe" : "mongosh";
        File.WriteAllText(Path.Combine(_directory, executable), string.Empty);
        var locator = new MongoToolsLocator(new MongoToolsOptions { Directory = _directory });

        var resolved = locator.Resolve("mongosh");

        resolved.Should().Be(Path.Combine(_directory, executable));
    }

    [Fact]
    public void Should_FallBackToBareName_When_NotConfiguredAndNotOnPath()
    {
        var locator = new MongoToolsLocator(new MongoToolsOptions());

        var resolved = locator.Resolve("definitely-not-a-tool-xyz");

        resolved.Should().Be("definitely-not-a-tool-xyz");
    }
}
