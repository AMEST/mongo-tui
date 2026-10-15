using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MongoTui.Application.DependencyInjection;
using MongoTui.Infrastructure.DependencyInjection;
using MongoTui.Infrastructure.FileSystem;
using MongoTui.Infrastructure.Ssh;
using MongoTui.TUI.Bootstrap;
using Serilog;

if (Environment.GetEnvironmentVariable("MONGOTUI_ASKPASS_PIPE") is { Length: > 0 } pipeName)
{
    return await new SshAskPassClient().RunAsync(pipeName, args.FirstOrDefault() ?? string.Empty, CancellationToken.None);
}

if (args.Contains("--version", StringComparer.Ordinal))
{
    await Console.Out.WriteLineAsync(ApplicationVersion.GetInformationalVersion());
    return 0;
}

Directory.CreateDirectory(AppPaths.LogsDirectory);

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.File(
        AppPaths.LogFilePattern,
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7)
    .CreateLogger();

AppDomain.CurrentDomain.UnhandledException += (_, args) =>
    Log.Fatal(args.ExceptionObject as Exception, "Unhandled exception");

TaskScheduler.UnobservedTaskException += (_, args) =>
{
    Log.Error(args.Exception, "Unobserved task exception");
    args.SetObserved();
};

try
{
    await using var provider = BuildServiceProvider();
    var host = provider.GetRequiredService<AppHost>();

    return await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
    return 1;
}
finally
{
    Log.CloseAndFlush();
}

static ServiceProvider BuildServiceProvider()
{
    var services = new ServiceCollection();

    services.AddLogging(builder => builder.AddSerilog(Log.Logger, dispose: false));
    services.AddApplication();
    services.AddInfrastructure();
    var executable = Environment.ProcessPath!;
    if (Path.GetFileNameWithoutExtension(executable).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
    {
        executable = Path.Combine(AppContext.BaseDirectory, "mongo-tui" + (OperatingSystem.IsWindows() ? ".exe" : string.Empty));
    }
    services.AddSingleton(new SshCliOptions(executable));
    services.AddTui();

    return services.BuildServiceProvider();
}
