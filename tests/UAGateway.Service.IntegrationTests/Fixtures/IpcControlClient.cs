using System.IO.Pipes;
using System.Text.Json;
using UAGateway.Core.Ipc;

namespace UAGateway.Service.IntegrationTests.Fixtures;

internal static class IpcControlClient
{
    public static async Task<IpcResponseEnvelope<TPayload>?> SendRequestAsync<TPayload>(
        string pipeName,
        string method,
        object payload,
        CancellationToken cancellationToken)
    {
        using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await client.ConnectAsync(5000, cancellationToken);

        using var reader = new StreamReader(client, leaveOpen: true);
        using var writer = new StreamWriter(client, leaveOpen: true) { AutoFlush = true };

        var request = new IpcRequestEnvelope<object>(
            RequestId: Guid.NewGuid().ToString("D"),
            TimestampUtc: DateTimeOffset.UtcNow,
            Method: method,
            Payload: payload,
            Client: new IpcClientMetadata("UAGateway.Service.IntegrationTests", "0.1.0"));

        var json = JsonSerializer.Serialize(request, IpcJsonSerializer.Options);
        await writer.WriteLineAsync(json);

        var responseLine = await reader.ReadLineAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(responseLine))
        {
            return null;
        }

        return JsonSerializer.Deserialize<IpcResponseEnvelope<TPayload>>(responseLine, IpcJsonSerializer.Options);
    }
}
