using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace UAGateway.Service;

internal sealed class GatewayWorker : BackgroundService
{
    private readonly ILogger<GatewayWorker> _logger;
    private readonly OpcUaBootstrapper _bootstrapper;
    private readonly UpstreamConnectionLifecycleManager _connectionLifecycleManager;

    public GatewayWorker(
        ILogger<GatewayWorker> logger,
        OpcUaBootstrapper bootstrapper,
        UpstreamConnectionLifecycleManager connectionLifecycleManager)
    {
        _logger = logger;
        _bootstrapper = bootstrapper;
        _connectionLifecycleManager = connectionLifecycleManager;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        GatewayLogMessages.WorkerStarting(_logger);

        _bootstrapper.Initialize();

        GatewayLogMessages.WorkerStarted(_logger);

        while (!stoppingToken.IsCancellationRequested)
        {
            _connectionLifecycleManager.RunIteration(DateTimeOffset.UtcNow);
            GatewayLogMessages.WorkerHeartbeat(_logger, DateTimeOffset.UtcNow);

            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _bootstrapper.Stop();
        await base.StopAsync(cancellationToken);
    }
}
