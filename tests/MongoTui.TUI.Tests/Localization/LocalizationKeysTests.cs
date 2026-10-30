using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Resources;
using AwesomeAssertions;
using MongoTui.Application.Localization;
using MongoTui.TUI.Localization;
using MongoTui.Web.Localization;

namespace MongoTui.TUI.Tests.Localization;

public sealed class LocalizationKeysTests
{
    [Fact]
    public void Should_DefineEveryResourceKey_WhenCatalogLoaded()
    {
        var keys = typeof(LocalizationKeys).GetNestedTypes()
            .SelectMany(type => type.GetFields(BindingFlags.Public | BindingFlags.Static))
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)
            .ToArray();
        var resources = ReadKeys(false, CultureInfo.InvariantCulture)
            .Concat(ReadKeys(true, CultureInfo.InvariantCulture))
            .Distinct();

        keys.Should().OnlyHaveUniqueItems().And.BeEquivalentTo(resources);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Should_ProvideSameKeys_WhenRussianTranslationLoaded(bool web)
    {
        var expected = ReadKeys(web, CultureInfo.InvariantCulture);

        var translated = ReadKeys(web, CultureInfo.GetCultureInfo("ru"));

        translated.Should().BeEquivalentTo(expected);
    }

    private static string[] ReadKeys(bool web, CultureInfo culture)
    {
        var assembly = web ? typeof(WebText).Assembly : typeof(ResxLocalizer).Assembly;
        var resources = new ResourceManager($"MongoTui.{(web ? "Web" : "TUI")}.Resources.Strings", assembly);
        try
        {
            var entries = resources.GetResourceSet(culture, createIfNotExists: true, tryParents: false)
                ?? throw new InvalidOperationException($"Missing resource set for '{culture.Name}'.");
            return entries.Cast<DictionaryEntry>().Select(entry => (string)entry.Key).ToArray();
        }
        finally { resources.ReleaseAllResources(); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Should_ProvideExplorerAndCreationStrings_WhenUsedByEitherInterface(bool web)
    {
        var keys = ReadKeys(web, CultureInfo.InvariantCulture);

        keys.Should().Contain(new[] { LocalizationKeys.Tree.Explorer, LocalizationKeys.Tree.ConnectionActions,
            LocalizationKeys.Tree.CreateDatabase, LocalizationKeys.Tree.Refresh, LocalizationKeys.Connection.Connect,
            LocalizationKeys.Connection.Edit, LocalizationKeys.Connection.Empty, LocalizationKeys.Connection.Remove,
            LocalizationKeys.Connection.DeleteTitle, LocalizationKeys.Connection.DeleteHint, LocalizationKeys.Common.Cancel,
            LocalizationKeys.Create.DatabaseTitle, LocalizationKeys.Create.DatabaseName,
            LocalizationKeys.Create.FirstCollection, LocalizationKeys.Create.DatabaseHint });
    }
}
