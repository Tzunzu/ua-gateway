using System.Text.Json;
using System.Text.Json.Serialization;
using UAGateway.Core.Configuration;
using UAGateway.Core.Diagnostics;
using UAGateway.Core.Health;

namespace UAGateway.Core.Ipc;

public static class IpcProtocol
{
    public const string Version = "1.0";
}

public static class IpcTransportDefaults
{
    public const string ControlPipeName = "ua-gateway-control";
    public const string EventPipeName = "ua-gateway-events";
}

public static class IpcMethodNames
{
    public const string SystemHandshake = "system.handshake";

    public const string HealthGetStartup = "health.getStartup";
    public const string SecurityGetBootstrap = "security.getBootstrap";
    public const string ConnectionsGetSnapshot = "connections.getSnapshot";
    public const string ConnectionsGetDraftConfig = "connections.getDraftConfig";
    public const string MappingGetSnapshot = "mapping.getSnapshot";

    public const string ConnectionsApplyDraftConfig = "connections.applyDraftConfig";
    public const string ConnectionsReloadDraftConfig = "connections.reloadDraftConfig";
    public const string RuntimeRequestReconnect = "runtime.requestReconnect";
    public const string DiagnosticsRequestSnapshotRefresh = "diagnostics.requestSnapshotRefresh";
}

public static class IpcEventCategories
{
    public const string ServiceLifecycle = "service.lifecycle";
    public const string SecurityBootstrap = "security.bootstrap";
    public const string ConnectionLifecycle = "connection.lifecycle";
    public const string ConfigApply = "config.apply";
    public const string ServerEndpoint = "server.endpoint";
    public const string DiagnosticsLog = "diagnostics.log";
}

public static class IpcErrorCodes
{
    public const string ProtocolVersionUnsupported = "ProtocolVersionUnsupported";
    public const string MethodNotFound = "MethodNotFound";
    public const string ValidationFailed = "ValidationFailed";
    public const string Conflict = "Conflict";
    public const string ServiceUnavailable = "ServiceUnavailable";
    public const string Timeout = "Timeout";
    public const string InternalError = "InternalError";
}

public enum IpcEventSeverity
{
    Information,
    Warning,
    Error,
    Critical,
}

public sealed record IpcClientMetadata(string Name, string Version);

public sealed record IpcRequestEnvelope<TPayload>(
    string RequestId,
    DateTimeOffset TimestampUtc,
    string Method,
    TPayload Payload,
    IpcClientMetadata Client);

public sealed record IpcResponseEnvelope<TPayload>(
    string RequestId,
    DateTimeOffset TimestampUtc,
    bool Success,
    string? ErrorCode,
    string? Message,
    TPayload Payload);

public sealed record IpcEventEnvelope<TPayload>(
    string EventId,
    DateTimeOffset TimestampUtc,
    string Category,
    string Name,
    IpcEventSeverity Severity,
    int ServiceEventId,
    string? CorrelationId,
    TPayload Payload);

public sealed record IpcHandshakeRequest(string RequestedProtocolVersion, bool SubscribeEvents);

public sealed record IpcCapabilitySet(
    bool EventStream,
    bool ApplyConfig,
    bool LiveLogEvents,
    bool SecurityActions);

public sealed record IpcHandshakeResponse(
    string ProtocolVersion,
    string ServiceVersion,
    IpcCapabilitySet Capabilities);

public sealed record IpcStartupHealthSnapshotPayload(bool Available, StartupHealthSnapshot? Snapshot);

public sealed record IpcSecurityBootstrapSnapshotPayload(bool Available, SecurityBootstrapDiagnosticsSnapshot? Snapshot);

public sealed record IpcConnectionSnapshotPayload(bool Available, ConnectionLifecycleDiagnosticsSnapshot? Snapshot);

public sealed record IpcMappingSnapshotPayload(bool Available, NamespaceMappingConfigurationDocument? Mapping);

public sealed record IpcDraftConfigPayload(UpstreamEndpointConfigurationDocument DraftDocument);

public sealed record IpcApplyDraftConfigRequest(UpstreamEndpointConfigurationDocument DraftDocument);

public sealed record IpcValidationIssue(string Code, string Target, string Message);

public sealed record IpcApplyDraftConfigResponse(
    string CorrelationId,
    bool Applied,
    IReadOnlyList<IpcValidationIssue> Issues);

public sealed record IpcReloadDraftConfigResponse(UpstreamEndpointConfigurationDocument DraftDocument);

public sealed record IpcRequestReconnectRequest(string? EndpointId, bool EnabledOnly = true);

public sealed record IpcRequestReconnectResponse(string CorrelationId, bool Accepted);

public sealed record IpcDiagnosticsSnapshotRefreshRequest(string? CorrelationId);

public sealed record IpcDiagnosticsSnapshotRefreshResponse(string CorrelationId, bool Accepted);

public sealed record IpcServiceEventPayload(string Message, string? EndpointId = null, string? Detail = null);

public static class IpcJsonSerializer
{
    public static JsonSerializerOptions Options { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase),
        },
    };
}