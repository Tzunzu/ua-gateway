using System.Threading.Channels;
using UAGateway.Core.Ipc;

namespace UAGateway.Service;

internal sealed class IpcEventStreamBroker
{
    private readonly Channel<IpcEventEnvelope<IpcServiceEventPayload>> _channel = Channel.CreateUnbounded<IpcEventEnvelope<IpcServiceEventPayload>>();

    public void Publish(
        string category,
        string name,
        IpcEventSeverity severity,
        int serviceEventId,
        string message,
        string? endpointId = null,
        string? detail = null,
        string? correlationId = null)
    {
        var envelope = new IpcEventEnvelope<IpcServiceEventPayload>(
            EventId: Guid.NewGuid().ToString("D"),
            TimestampUtc: DateTimeOffset.UtcNow,
            Category: category,
            Name: name,
            Severity: severity,
            ServiceEventId: serviceEventId,
            CorrelationId: correlationId,
            Payload: new IpcServiceEventPayload(message, endpointId, detail));

        _channel.Writer.TryWrite(envelope);
    }

    public IAsyncEnumerable<IpcEventEnvelope<IpcServiceEventPayload>> ReadAllAsync(CancellationToken cancellationToken)
    {
        return _channel.Reader.ReadAllAsync(cancellationToken);
    }
}
