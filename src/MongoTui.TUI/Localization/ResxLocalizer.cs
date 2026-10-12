using System.Globalization;
using System.Resources;
using MongoTui.Application.Abstractions.Localization;

namespace MongoTui.TUI.Localization;

/// <summary><see cref="ILocalizer"/> backed by the presentation <c>Strings</c> resources.</summary>
public sealed class ResxLocalizer : ILocalizer
{
    private readonly ResourceManager _resources =
        new("MongoTui.TUI.Resources.Strings", typeof(ResxLocalizer).Assembly);

    private CultureInfo _culture = CultureInfo.CurrentUICulture;

    public string CurrentLanguage => _culture.TwoLetterISOLanguageName;

    public void SetLanguage(string language) => _culture = new CultureInfo(language);

    public string Get(string key) => _resources.GetString(key, _culture) ?? key;

    public string Format(string key, params object[] arguments) =>
        string.Format(_culture, Get(key), arguments);
}
