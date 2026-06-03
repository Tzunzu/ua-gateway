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
        GatewayLogMessages.WorkerStarting(_logger);

        _bootstrapper.Initialize();

        GatewayLogMessages.WorkerStarted(_logger);

        while (!stoppingToken.IsCancellationRequested)
        {
            GatewayLogMessages.WorkerHeartbeat(_logger, DateTimeOffset.UtcNow);

            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
        }
    }
}
