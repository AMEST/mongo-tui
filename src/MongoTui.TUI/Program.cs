using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MongoTui.Application.DependencyInjection;
using MongoTui.Infrastructure.DependencyInjection;
using MongoTui.Infrastructure.FileSystem;
using MongoTui.TUI.Bootstrap;
using Serilog;

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
    using var provider = BuildServiceProvider();
    var host = provider.GetRequiredService<AppHost>();

    return host.Run();
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
    services.AddTui();

    return services.BuildServiceProvider();
}
