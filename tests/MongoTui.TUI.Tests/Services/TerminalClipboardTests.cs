using System.Text;
using AwesomeAssertions;
using MongoTui.TUI.Services;
using Terminal.Gui.Drivers;

namespace MongoTui.TUI.Tests.Services;

public sealed class TerminalClipboardTests
{
    [Fact]
    public void Should_UsePlatformClipboard_When_Supported()
    {
        var platform = new FakeClipboard();
        var writes = new List<string>();
        var clipboard = new TerminalClipboard(platform, writes.Add);

        var copied = clipboard.TrySetClipboardData("hello");

        copied.Should().BeTrue();
        clipboard.TryGetClipboardData(out var result).Should().BeTrue();
        result.Should().Be("hello");
        writes.Should().BeEmpty();
    }

    [Fact]
    public void Should_FallBackToOsc52_When_PlatformUnsupported()
    {
        var platform = new FakeClipboard(isSupportedAlwaysFalse: true);
        var writes = new List<string>();
        var clipboard = new TerminalClipboard(platform, writes.Add);

        var copied = clipboard.TrySetClipboardData("hi");

        copied.Should().BeTrue();
        writes.Should().ContainSingle().Which.Should().Be("\u001b]52;c;aGk=\u0007");
    }

    [Fact]
    public void Should_EncodeUtf8MultilineText_When_FallingBackToOsc52()
    {
        const string text = "привет\nмир";
        var writes = new List<string>();
        var clipboard = new TerminalClipboard(platform: null, writes.Add);

        clipboard.TrySetClipboardData(text);

        var expected = Convert.ToBase64String(Encoding.UTF8.GetBytes(text));
        writes.Should().ContainSingle().Which.Should().Be($"\u001b]52;c;{expected}\u0007");
    }

    [Fact]
    public void Should_ReturnTrue_When_PlatformIsUnavailable()
    {
        var clipboard = new TerminalClipboard(platform: null, _ => { });

        clipboard.IsSupported.Should().BeTrue();
        clipboard.TrySetClipboardData("text").Should().BeTrue();
    }

    [Fact]
    public void Should_NotReadClipboard_When_OnlyFallbackAvailable()
    {
        var clipboard = new TerminalClipboard(platform: null, _ => { });

        clipboard.TryGetClipboardData(out var result).Should().BeFalse();
        result.Should().BeEmpty();
    }

    [Fact]
    public void Should_ReportFailure_When_FallbackWriteThrows()
    {
        var clipboard = new TerminalClipboard(platform: null, _ => throw new IOException("no tty"));

        clipboard.TrySetClipboardData("text").Should().BeFalse();
    }

    [Fact]
    public void Should_Throw_When_SetUnsupportedAndFallbackFails()
    {
        var clipboard = new TerminalClipboard(platform: null, _ => throw new IOException("no tty"));

        var act = () => clipboard.SetClipboardData("text");

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void Should_Throw_When_GetWithoutPlatform()
    {
        var clipboard = new TerminalClipboard(platform: null, _ => { });

        var act = () => clipboard.GetClipboardData();

        act.Should().Throw<NotSupportedException>();
    }
}
