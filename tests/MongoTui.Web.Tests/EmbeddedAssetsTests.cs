using AwesomeAssertions;
using Microsoft.Extensions.FileProviders;
using MongoTui.Web.Bootstrap;

namespace MongoTui.Web.Tests;

public sealed class EmbeddedAssetsTests
{
    [Theory]
    [InlineData("wwwroot", "app.css")]
    [InlineData("wwwroot", "editor.js")]
    [InlineData("wwwroot", "theme.js")]
    [InlineData("wwwroot", "auth.js")]
    [InlineData("wwwroot", "favicon.svg")]
    [InlineData("framework", "blazor.web.js")]
    public void Should_ReadAssetFromAssembly_WhenPhysicalFilesAreUnavailable(string root, string name)
    {
        var provider = new ManifestEmbeddedFileProvider(typeof(WebHost).Assembly, root);

        var file = provider.GetFileInfo(name);

        file.Exists.Should().BeTrue();
        file.PhysicalPath.Should().BeNull();
        using var content = file.CreateReadStream();
        content.Length.Should().BeGreaterThan(0);
    }
}
