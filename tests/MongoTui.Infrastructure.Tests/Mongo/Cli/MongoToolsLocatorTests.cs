using AwesomeAssertions;
using MongoTui.Infrastructure.Mongo.Cli;
using MongoTui.Infrastructure.Options;

namespace MongoTui.Infrastructure.Tests.Mongo.Cli;

public sealed class MongoToolsLocatorTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"mongotools-{Guid.NewGuid():N}");
    private readonly string _bundledDirectory;

    public MongoToolsLocatorTests()
    {
        Directory.CreateDirectory(_directory);
        _bundledDirectory = Path.Combine(_directory, "thirdparty");
        Directory.CreateDirectory(_bundledDirectory);
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public void Should_ResolveFromConfiguredDirectory_When_ExecutableExists()
    {
        var executable = ExecutableName("mongosh");
        File.WriteAllText(Path.Combine(_directory, executable), string.Empty);
        var locator = new MongoToolsLocator(new MongoToolsOptions { Directory = _directory });

        var resolved = locator.Resolve("mongosh");

        resolved.Should().Be(Path.Combine(_directory, executable));
    }

    [Fact]
    public void Should_ResolveFromConfiguredDirectory_BeforeBundledDirectory_When_BothExecutablesExist()
    {
        var executable = ExecutableName("configured-tool-xyz");
        File.WriteAllText(Path.Combine(_directory, executable), string.Empty);
        File.WriteAllText(Path.Combine(_bundledDirectory, executable), string.Empty);
        var locator = new MongoToolsLocator(new MongoToolsOptions
        {
            Directory = _directory,
            BundledDirectory = _bundledDirectory,
        });

        var resolved = locator.Resolve("configured-tool-xyz");

        resolved.Should().Be(Path.Combine(_directory, executable));
    }

    [Fact]
    public void Should_ResolveFromBundledDirectory_BeforePath_When_OnWindowsAndExecutableExists()
    {
        var executable = ExecutableName("bundled-tool-xyz");
        File.WriteAllText(Path.Combine(_directory, executable), string.Empty);
        File.WriteAllText(Path.Combine(_bundledDirectory, executable), string.Empty);
        var locator = new MongoToolsLocator(new MongoToolsOptions { BundledDirectory = _bundledDirectory });
        var originalPath = Environment.GetEnvironmentVariable("PATH");

        try
        {
            Environment.SetEnvironmentVariable("PATH", _directory);

            var resolved = locator.Resolve("bundled-tool-xyz");

            if (OperatingSystem.IsWindows())
            {
                resolved.Should().Be(Path.Combine(_bundledDirectory, executable));
            }
            else
            {
                resolved.Should().Be(Path.Combine(_directory, executable));
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", originalPath);
        }
    }

    [Fact]
    public void Should_ResolveFromPath_When_BundledDirectoryHasNoExecutable()
    {
        var executable = ExecutableName("path-tool-xyz");
        File.WriteAllText(Path.Combine(_directory, executable), string.Empty);
        var locator = new MongoToolsLocator(new MongoToolsOptions { BundledDirectory = _bundledDirectory });
        var originalPath = Environment.GetEnvironmentVariable("PATH");

        try
        {
            Environment.SetEnvironmentVariable("PATH", _directory);

            var resolved = locator.Resolve("path-tool-xyz");

            resolved.Should().Be(Path.Combine(_directory, executable));
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", originalPath);
        }
    }

    [Fact]
    public void Should_FallBackToBareName_When_NotConfiguredAndNotOnPath()
    {
        var locator = new MongoToolsLocator(new MongoToolsOptions { BundledDirectory = _bundledDirectory });

        var resolved = locator.Resolve("definitely-not-a-tool-xyz");

        resolved.Should().Be("definitely-not-a-tool-xyz");
    }

    private static string ExecutableName(string toolName) =>
        OperatingSystem.IsWindows() ? toolName + ".exe" : toolName;
}
