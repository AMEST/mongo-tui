using AwesomeAssertions;
using MongoTui.Application.Models;

namespace MongoTui.Application.Tests.Models;

public sealed class ScriptOutputTests
{
    [Fact]
    public async Task Should_PreserveCompleteLog_WhenPreviewOverflows()
    {
        await using var output = new ScriptOutput();
        var first = "  start\r\n" + new string('я', ScriptOutput.PreviewCharacters - 16);
        var second = "ошибка 🚀\n";
        const string last = "  done  ";
        await output.AppendAsync(new(first, false));
        await output.AppendAsync(new(second, true));
        await output.AppendAsync(new(last, false));

        await using var stream = await output.OpenReadAsync(CancellationToken.None);
        using var reader = new StreamReader(stream);
        var transcript = await reader.ReadToEndAsync();

        transcript.Should().Be(first + "\n[stderr] " + second + last);
        var preview = output.ReadPreview();
        preview.HasEarlierOutput.Should().BeTrue();
        preview.Text.Length.Should().BeLessThanOrEqualTo(ScriptOutput.PreviewCharacters);
        preview.Text.Should().EndWith(last);
    }
}
