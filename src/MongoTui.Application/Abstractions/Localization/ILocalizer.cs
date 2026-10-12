namespace MongoTui.Application.Abstractions.Localization;

/// <summary>Resolves UI strings for the current language.</summary>
public interface ILocalizer
{
    string Get(string key);

    string Format(string key, params object[] arguments);

    string CurrentLanguage { get; }

    void SetLanguage(string language);
}
