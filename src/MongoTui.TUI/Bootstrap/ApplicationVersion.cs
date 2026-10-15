using System.Reflection;

namespace MongoTui.TUI.Bootstrap;

internal static class ApplicationVersion
{
    public static string GetInformationalVersion() =>
        Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? string.Empty;
}
