using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace UAGateway.Service;

internal static class Program
{
    public static void Main(string[] args)
    {
        var host = Host.CreateApplicationBuilder(args);
        var logDirectory = Path.Combine(AppContext.BaseDirectory, "logs");
        Directory.CreateDirectory(logDirectory);

        Log.Logger = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .WriteTo.File(
                path: Path.Combine(logDirectory, "ua-gateway-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                fileSizeLimitBytes: 10 * 1024 * 1024,
                rollOnFileSizeLimit: true,
                shared: true)
            .CreateLogger();

        host.Services.AddSerilog(Log.Logger, dispose: true);
        host.Services.AddWindowsService();
        host.Services.AddSingleton<StartupHealthState>();
        host.Services.AddSingleton<OpcUaBootstrapper>();
        host.Services.AddHostedService<GatewayWorker>();

        try
        {
            using var app = host.Build();
            app.Run();
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
}
