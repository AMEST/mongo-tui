using AwesomeAssertions;
using MongoTui.TUI.Editor;

namespace MongoTui.TUI.Tests.Editor;

public sealed class SnippetsTests
{
    [Fact]
    public void Should_ContainFindSnippet()
    {
        Snippets.All.Should().Contain(snippet => snippet.Title == "find");
    }

    [Fact]
    public void Should_HaveNonEmptyBodies()
    {
        Snippets.All.Should().OnlyContain(snippet => !string.IsNullOrWhiteSpace(snippet.Body));
    }
}
