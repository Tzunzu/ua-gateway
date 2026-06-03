using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace UAGateway.Service;

internal static class Program
{
    public static void Main(string[] args)
    {
        var host = Host.CreateApplicationBuilder(args);
        host.Services.AddWindowsService();
        host.Services.AddSingleton<OpcUaBootstrapper>();
        host.Services.AddHostedService<GatewayWorker>();
        using var app = host.Build();
        app.Run();
    }
}
