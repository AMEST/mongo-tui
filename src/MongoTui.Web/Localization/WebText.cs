using System.Globalization;
using System.Resources;
using MongoTui.Application.Localization;
using MongoTui.Application.Models;

namespace MongoTui.Web.Localization;

public sealed class WebText
{
    private readonly ResourceManager _resources = new("MongoTui.Web.Resources.Strings", typeof(WebText).Assembly);
    public string Language { get; private set; } = UiLanguages.English;
    public string this[string key] => Get(key, Language);
    public string Get(string key, string language) => _resources.GetString(key, CultureInfo.GetCultureInfo(language)) ?? key;
    public void SetLanguage(string? language) => Language = language == UiLanguages.Russian ? UiLanguages.Russian : UiLanguages.English;
    public string Format(string key, params object[] values) => string.Format(CultureInfo.GetCultureInfo(Language), this[key], values);

    public string SecurityModeName(SecurityMode? mode) => this[mode switch
    {
        SecurityMode.None => LocalizationKeys.Security.None,
        SecurityMode.Password => LocalizationKeys.Security.Password,
        SecurityMode.Keyring => LocalizationKeys.Security.Keyring,
        _ => LocalizationKeys.Security.Mode,
    }];
}
