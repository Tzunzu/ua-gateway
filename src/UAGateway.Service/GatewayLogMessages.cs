using Microsoft.Extensions.Logging;
using UAGateway.Core.Diagnostics;

namespace UAGateway.Service;

internal static partial class GatewayLogMessages
{
    [LoggerMessage(
        EventId = UAGatewayEventIds.MappingAndConfiguration.ConfigApplyStarted,
        EventName = nameof(UAGatewayEventIds.MappingAndConfiguration.ConfigApplyStarted),
        Level = LogLevel.Information,
        Message = "Configuration apply flow started. CorrelationId: {CorrelationId}")]
    public static partial void ConfigApplyStarted(ILogger logger, string correlationId);

    [LoggerMessage(
        EventId = UAGatewayEventIds.MappingAndConfiguration.ConfigApplyCompleted,
        EventName = nameof(UAGatewayEventIds.MappingAndConfiguration.ConfigApplyCompleted),
        Level = LogLevel.Information,
        Message = "Configuration apply flow completed. CorrelationId: {CorrelationId}")]
    public static partial void ConfigApplyCompleted(ILogger logger, string correlationId);

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
        EventId = UAGatewayEventIds.ConnectionLifecycle.ReconnectFlowStarted,
        EventName = nameof(UAGatewayEventIds.ConnectionLifecycle.ReconnectFlowStarted),
        Level = LogLevel.Information,
        Message = "Reconnect flow started. CorrelationId: {CorrelationId}")]
    public static partial void ReconnectFlowStarted(ILogger logger, string correlationId);

    [LoggerMessage(
        EventId = UAGatewayEventIds.ConnectionLifecycle.NoUpstreamEndpointsConfigured,
        EventName = nameof(UAGatewayEventIds.ConnectionLifecycle.NoUpstreamEndpointsConfigured),
        Level = LogLevel.Warning,
        Message = "No upstream endpoints are configured yet; connection lifecycle remains idle. CorrelationId: {CorrelationId}")]
    public static partial void NoUpstreamEndpointsConfigured(ILogger logger, string correlationId);

    [LoggerMessage(
        EventId = UAGatewayEventIds.ConnectionLifecycle.ReconnectFlowIdleNoEndpoints,
        EventName = nameof(UAGatewayEventIds.ConnectionLifecycle.ReconnectFlowIdleNoEndpoints),
        Level = LogLevel.Information,
        Message = "Reconnect flow completed in idle state with no endpoints. CorrelationId: {CorrelationId}")]
    public static partial void ReconnectFlowIdleNoEndpoints(ILogger logger, string correlationId);
}
