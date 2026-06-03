using Microsoft.Extensions.Logging;
using UAGateway.Core.Diagnostics;

namespace UAGateway.Service;

internal static partial class GatewayLogMessages
{
    [LoggerMessage(
        EventId = UAGatewayEventIds.ServiceLifecycle.WorkerStarting,
        EventName = nameof(UAGatewayEventIds.ServiceLifecycle.WorkerStarting),
        Level = LogLevel.Information,
        Message = "UA Gateway service worker starting.")]
    public static partial void WorkerStarting(ILogger logger);

    [LoggerMessage(
        EventId = UAGatewayEventIds.ServiceLifecycle.WorkerStarted,
        EventName = nameof(UAGatewayEventIds.ServiceLifecycle.WorkerStarted),
        Level = LogLevel.Information,
        Message = "UA Gateway service worker started.")]
    public static partial void WorkerStarted(ILogger logger);

    [LoggerMessage(
        EventId = UAGatewayEventIds.ServiceLifecycle.WorkerHeartbeat,
        EventName = nameof(UAGatewayEventIds.ServiceLifecycle.WorkerHeartbeat),
        Level = LogLevel.Information,
        Message = "UA Gateway service heartbeat: {UtcNow}")]
    public static partial void WorkerHeartbeat(ILogger logger, DateTimeOffset utcNow);

    [LoggerMessage(
        EventId = UAGatewayEventIds.ServiceLifecycle.OpcUaBootstrapInitialized,
        EventName = nameof(UAGatewayEventIds.ServiceLifecycle.OpcUaBootstrapInitialized),
        Level = LogLevel.Information,
        Message = "OPC UA stack initialized. Baseline status code: {StatusCode}")]
    public static partial void OpcUaBootstrapInitialized(ILogger logger, uint statusCode);

    [LoggerMessage(
        EventId = UAGatewayEventIds.ConnectionLifecycle.ConnectionManagerInitialized,
        EventName = nameof(UAGatewayEventIds.ConnectionLifecycle.ConnectionManagerInitialized),
        Level = LogLevel.Information,
        Message = "Upstream connection lifecycle manager initialized.")]
    public static partial void ConnectionManagerInitialized(ILogger logger);

    [LoggerMessage(
        EventId = UAGatewayEventIds.ConnectionLifecycle.NoUpstreamEndpointsConfigured,
        EventName = nameof(UAGatewayEventIds.ConnectionLifecycle.NoUpstreamEndpointsConfigured),
        Level = LogLevel.Warning,
        Message = "No upstream endpoints are configured yet; connection lifecycle remains idle.")]
    public static partial void NoUpstreamEndpointsConfigured(ILogger logger);
}
