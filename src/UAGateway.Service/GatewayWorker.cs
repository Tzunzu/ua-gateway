using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace UAGateway.Service;

internal sealed class GatewayWorker : BackgroundService
{
    private readonly ILogger<GatewayWorker> _logger;
    private readonly OpcUaBootstrapper _bootstrapper;

    public GatewayWorker(ILogger<GatewayWorker> logger, OpcUaBootstrapper bootstrapper)
    {
        _logger = logger;
        _bootstrapper = bootstrapper;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("UA Gateway service worker starting.");
        _bootstrapper.Initialize();
        _logger.LogInformation("UA Gateway service worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("UA Gateway service heartbeat: {UtcNow}", DateTimeOffset.UtcNow);
            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
        }
    }
}
