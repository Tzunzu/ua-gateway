using Microsoft.Extensions.Logging;
using UAGateway.Core.Diagnostics;

namespace UAGateway.Service;

internal static partial class GatewayLogMessages
{
    [LoggerMessage(
        EventId = UAGatewayEventIds.SecurityAndTrust.SecurityBootstrapStarted,
        EventName = nameof(UAGatewayEventIds.SecurityAndTrust.SecurityBootstrapStarted),
        Level = LogLevel.Information,
        Message = "Security bootstrap started. CorrelationId: {CorrelationId}")]
    public static partial void SecurityBootstrapStarted(ILogger logger, string correlationId);

    [LoggerMessage(
        EventId = UAGatewayEventIds.SecurityAndTrust.ApplicationCertificateReady,
        EventName = nameof(UAGatewayEventIds.SecurityAndTrust.ApplicationCertificateReady),
        Level = LogLevel.Information,
        Message = "Application certificate is available. Thumbprint: {Thumbprint}")]
    public static partial void ApplicationCertificateReady(ILogger logger, string thumbprint);

    [LoggerMessage(
        EventId = UAGatewayEventIds.SecurityAndTrust.TrustStoreStateObserved,
        EventName = nameof(UAGatewayEventIds.SecurityAndTrust.TrustStoreStateObserved),
        Level = LogLevel.Information,
        Message = "Trust store state observed. TrustedPeerCount: {TrustedPeerCount}, TrustedIssuerCount: {TrustedIssuerCount}, RejectedCount: {RejectedCount}")]
    public static partial void TrustStoreStateObserved(ILogger logger, int trustedPeerCount, int trustedIssuerCount, int rejectedCount);

    [LoggerMessage(
        EventId = UAGatewayEventIds.SecurityAndTrust.TrustPolicyDefaultsApplied,
        EventName = nameof(UAGatewayEventIds.SecurityAndTrust.TrustPolicyDefaultsApplied),
        Level = LogLevel.Information,
        Message = "Trust policy defaults applied. AutoAcceptUntrusted: {AutoAcceptUntrusted}, RejectSha1: {RejectSha1}, MinimumKeySize: {MinimumKeySize}")]
    public static partial void TrustPolicyDefaultsApplied(ILogger logger, bool autoAcceptUntrusted, bool rejectSha1, ushort minimumKeySize);

    [LoggerMessage(
        EventId = UAGatewayEventIds.SecurityAndTrust.NoTrustedPeersConfigured,
        EventName = nameof(UAGatewayEventIds.SecurityAndTrust.NoTrustedPeersConfigured),
        Level = LogLevel.Warning,
        Message = "No trusted peer certificates are configured. New peers will remain untrusted until explicitly added.")]
    public static partial void NoTrustedPeersConfigured(ILogger logger);

    [LoggerMessage(
        EventId = UAGatewayEventIds.SecurityAndTrust.UntrustedCertificateRejected,
        EventName = nameof(UAGatewayEventIds.SecurityAndTrust.UntrustedCertificateRejected),
        Level = LogLevel.Warning,
        Message = "Untrusted certificate rejected. Thumbprint: {Thumbprint}, StatusCode: {StatusCode}")]
    public static partial void UntrustedCertificateRejected(ILogger logger, string thumbprint, uint statusCode);

    [LoggerMessage(
        EventId = UAGatewayEventIds.SecurityAndTrust.SecurityDiagnosticsPublished,
        EventName = nameof(UAGatewayEventIds.SecurityAndTrust.SecurityDiagnosticsPublished),
        Level = LogLevel.Information,
        Message = "Security diagnostics snapshot published at {Path}.")]
    public static partial void SecurityDiagnosticsPublished(ILogger logger, string path);

    [LoggerMessage(
        EventId = UAGatewayEventIds.SecurityAndTrust.SecurityBootstrapFailed,
        EventName = nameof(UAGatewayEventIds.SecurityAndTrust.SecurityBootstrapFailed),
        Level = LogLevel.Error,
        Message = "Security bootstrap failed. Error: {ErrorMessage}")]
    public static partial void SecurityBootstrapFailed(ILogger logger, string errorMessage, Exception exception);

    [LoggerMessage(
        EventId = UAGatewayEventIds.MappingAndConfiguration.OpcUaConfigurationBuildStarted,
        EventName = nameof(UAGatewayEventIds.MappingAndConfiguration.OpcUaConfigurationBuildStarted),
        Level = LogLevel.Information,
        Message = "OPC UA application configuration build started. CorrelationId: {CorrelationId}")]
    public static partial void OpcUaConfigurationBuildStarted(ILogger logger, string correlationId);

    [LoggerMessage(
        EventId = UAGatewayEventIds.MappingAndConfiguration.OpcUaConfigurationValidated,
        EventName = nameof(UAGatewayEventIds.MappingAndConfiguration.OpcUaConfigurationValidated),
        Level = LogLevel.Information,
        Message = "OPC UA application configuration validated. ApplicationUri: {ApplicationUri}")]
    public static partial void OpcUaConfigurationValidated(ILogger logger, string applicationUri);

    [LoggerMessage(
        EventId = UAGatewayEventIds.MappingAndConfiguration.OpcUaConfigurationValidationFailed,
        EventName = nameof(UAGatewayEventIds.MappingAndConfiguration.OpcUaConfigurationValidationFailed),
        Level = LogLevel.Error,
        Message = "OPC UA application configuration validation failed. Error: {ErrorMessage}")]
    public static partial void OpcUaConfigurationValidationFailed(ILogger logger, string errorMessage, Exception exception);

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
        EventId = UAGatewayEventIds.ServiceLifecycle.StartupHealthStateChanged,
        EventName = nameof(UAGatewayEventIds.ServiceLifecycle.StartupHealthStateChanged),
        Level = LogLevel.Information,
        Message = "Startup health state changed. Status: {Status}, Reason: {Reason}, CorrelationId: {CorrelationId}")]
    public static partial void StartupHealthStateChanged(ILogger logger, string status, string reason, string correlationId);

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
