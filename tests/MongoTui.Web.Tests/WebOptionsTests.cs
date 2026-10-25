using AwesomeAssertions;
using MongoTui.Web.Bootstrap;

namespace MongoTui.Web.Tests;

public sealed class WebOptionsTests
{
    [Fact]
    public void Should_ConfigureLocalHost_WhenPortAndOpenPassed()
    {
        var options = WebOptions.Parse(["--port", "9000", "--open"]);

        options.Url.Should().Be("http://127.0.0.1:9000");
        options.OpenBrowser.Should().BeTrue();
    }

    [Theory]
    [InlineData("--port", "0")]
    [InlineData("--port", "65536")]
    [InlineData("--port", "no")]
    [InlineData("--host", "0.0.0.0")]
    public void ShouldThrow_ArgumentException_WhenUnsupportedArguments(string name, string value)
    {
        var act = () => WebOptions.Parse([name, value]);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ShouldThrow_ArgumentException_WhenPortMissing()
    {
        var act = () => WebOptions.Parse(["--port"]);

        act.Should().Throw<ArgumentException>();
    }
}
