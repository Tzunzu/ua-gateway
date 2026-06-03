using System.Text.Json;
using UAGateway.Core.Ipc;
using Xunit;

namespace UAGateway.Core.Tests;

public sealed class IpcContractTests
{
    [Fact]
    public void MethodNames_AreUnique()
    {
        var methods = new[]
        {
            IpcMethodNames.SystemHandshake,
            IpcMethodNames.HealthGetStartup,
            IpcMethodNames.SecurityGetBootstrap,
            IpcMethodNames.ConnectionsGetSnapshot,
            IpcMethodNames.ConnectionsGetDraftConfig,
            IpcMethodNames.MappingGetSnapshot,
            IpcMethodNames.ConnectionsApplyDraftConfig,
            IpcMethodNames.ConnectionsReloadDraftConfig,
            IpcMethodNames.RuntimeRequestReconnect,
            IpcMethodNames.DiagnosticsRequestSnapshotRefresh,
        };

        Assert.Equal(methods.Length, methods.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void HandshakeEnvelope_RoundTripsWithJsonOptions()
    {
        var request = new IpcRequestEnvelope<IpcHandshakeRequest>(
            RequestId: Guid.NewGuid().ToString("D"),
            TimestampUtc: DateTimeOffset.UtcNow,
            Method: IpcMethodNames.SystemHandshake,
            Payload: new IpcHandshakeRequest(IpcProtocol.Version, SubscribeEvents: true),
            Client: new IpcClientMetadata("UAGateway.UI", "0.1.0"));

        var json = JsonSerializer.Serialize(request, IpcJsonSerializer.Options);
        var roundTrip = JsonSerializer.Deserialize<IpcRequestEnvelope<IpcHandshakeRequest>>(json, IpcJsonSerializer.Options);

        Assert.NotNull(roundTrip);
        Assert.Equal(request.RequestId, roundTrip!.RequestId);
        Assert.Equal(request.Method, roundTrip.Method);
        Assert.Equal(IpcProtocol.Version, roundTrip.Payload.RequestedProtocolVersion);
        Assert.True(roundTrip.Payload.SubscribeEvents);
        Assert.Equal("UAGateway.UI", roundTrip.Client.Name);
    }

    [Fact]
    public void EventEnvelope_SerializesSeverityAsCamelCaseString()
    {
        var evt = new IpcEventEnvelope<IpcServiceEventPayload>(
            EventId: Guid.NewGuid().ToString("D"),
            TimestampUtc: DateTimeOffset.UtcNow,
            Category: IpcEventCategories.ConnectionLifecycle,
            Name: "EndpointReconnecting",
            Severity: IpcEventSeverity.Warning,
            ServiceEventId: 2004,
            CorrelationId: "cfg-apply-1",
            Payload: new IpcServiceEventPayload("reconnecting", "endpoint-1", "retry=2"));

        var json = JsonSerializer.Serialize(evt, IpcJsonSerializer.Options);

        Assert.Contains("\"severity\":\"warning\"", json, StringComparison.Ordinal);
        Assert.Contains("\"category\":\"connection.lifecycle\"", json, StringComparison.Ordinal);
    }
}