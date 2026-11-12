using System.Diagnostics;

namespace MongoTui.TUI.Tests.Bootstrap;

internal sealed class WebCommandLineProcess : IAsyncDisposable
{
    public string DataDirectory { get; } = Path.Combine(Path.GetTempPath(), "mongotui-web-cli-" + Guid.NewGuid().ToString("N"));
    public Process Process { get; }

    public WebCommandLineProcess(int port, string environment, Action<ProcessStartInfo>? configure = null)
    {
        var info = new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory,
            "mongo-tui" + (OperatingSystem.IsWindows() ? ".exe" : string.Empty)))
        {
            UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true,
            CreateNoWindow = true, WorkingDirectory = DataDirectory,
        };
        Directory.CreateDirectory(DataDirectory);
        foreach (var key in info.Environment.Keys.Where(IsHostSetting).ToArray()) info.Environment.Remove(key);
        info.Environment["MONGOTUI_DATA_DIR"] = DataDirectory;
        info.Environment["DOTNET_ENVIRONMENT"] = environment;
        info.Environment["PATH"] = DataDirectory;
        info.Environment["LANG"] = "en_US.UTF-8";
        info.Environment["LC_ALL"] = "en_US.UTF-8";
        info.ArgumentList.Add("web");
        info.ArgumentList.Add("--port");
        info.ArgumentList.Add(port.ToString(System.Globalization.CultureInfo.InvariantCulture));
        configure?.Invoke(info);
        Process = Process.Start(info)!;
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (!Process.HasExited) Process.Kill(entireProcessTree: true);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await Process.WaitForExitAsync(timeout.Token);
        }
        finally
        {
            Process.Dispose();
            Directory.Delete(DataDirectory, recursive: true);
        }
    }

    private static bool IsHostSetting(string key) =>
        key.StartsWith("ASPNETCORE_", StringComparison.OrdinalIgnoreCase)
        || (key.StartsWith("DOTNET_", StringComparison.OrdinalIgnoreCase)
            && !key.StartsWith("DOTNET_ROOT", StringComparison.OrdinalIgnoreCase))
        || key.StartsWith("MONGOTUI_", StringComparison.OrdinalIgnoreCase)
        || key.StartsWith("Kestrel__", StringComparison.OrdinalIgnoreCase)
        || key.Equals("URLS", StringComparison.OrdinalIgnoreCase);
}
