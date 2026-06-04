using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using UAGateway.Core.Diagnostics;
using UAGateway.Core.Ipc;

namespace UAGateway.Service;

internal sealed class GatewayWorker : BackgroundService
{
    private readonly ILogger<GatewayWorker> _logger;
    private readonly OpcUaBootstrapper _bootstrapper;
    private readonly UpstreamConnectionLifecycleManager _connectionLifecycleManager;
    private readonly IpcEventStreamBroker _eventStreamBroker;

    public GatewayWorker(
        ILogger<GatewayWorker> logger,
        OpcUaBootstrapper bootstrapper,
        UpstreamConnectionLifecycleManager connectionLifecycleManager,
        IpcEventStreamBroker eventStreamBroker)
    {
        _logger = logger;
        _bootstrapper = bootstrapper;
        _connectionLifecycleManager = connectionLifecycleManager;
        _eventStreamBroker = eventStreamBroker;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        GatewayLogMessages.WorkerStarting(_logger);
        _eventStreamBroker.Publish(
            category: IpcEventCategories.ServiceLifecycle,
            name: nameof(UAGatewayEventIds.ServiceLifecycle.WorkerStarting),
            severity: IpcEventSeverity.Information,
            serviceEventId: UAGatewayEventIds.ServiceLifecycle.WorkerStarting,
            message: "UA Gateway service worker starting.");

        try
        {
            _bootstrapper.Initialize();
        }
        catch (Exception ex)
        {
            // Initialize() already published a Faulted health snapshot before throwing.
            // We catch here so the background service task stays alive and the IPC
            // control server can continue serving the Faulted health state to the UI.
            _logger.LogCritical(ex, "UA Gateway startup faulted. Service is running in a degraded state. Check startup health via the UI or logs.");
        }

        GatewayLogMessages.WorkerStarted(_logger);
        _eventStreamBroker.Publish(
            category: IpcEventCategories.ServiceLifecycle,
            name: nameof(UAGatewayEventIds.ServiceLifecycle.WorkerStarted),
            severity: IpcEventSeverity.Information,
            serviceEventId: UAGatewayEventIds.ServiceLifecycle.WorkerStarted,
            message: "UA Gateway service worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            _connectionLifecycleManager.RunIteration(DateTimeOffset.UtcNow);
            GatewayLogMessages.WorkerHeartbeat(_logger, DateTimeOffset.UtcNow);
            _eventStreamBroker.Publish(
                category: IpcEventCategories.ServiceLifecycle,
                name: nameof(UAGatewayEventIds.ServiceLifecycle.WorkerHeartbeat),
                severity: IpcEventSeverity.Information,
                serviceEventId: UAGatewayEventIds.ServiceLifecycle.WorkerHeartbeat,
                message: "UA Gateway service heartbeat.");

            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _bootstrapper.Stop();
        await base.StopAsync(cancellationToken);
    }
}
