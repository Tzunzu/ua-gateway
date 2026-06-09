using System.IO.Pipes;
using System.Text.Json;
using UAGateway.Core.Configuration;
using UAGateway.Core.Ipc;

namespace UAGateway.UI.Services;

internal sealed class IpcControlClient
{
    private const string ClientName = "UAGateway.UI";
    private const string ClientVersion = "0.1.0";

    public async Task<IpcHandshakeResponse?> TryHandshakeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = new NamedPipeClientStream(
                serverName: ".",
                pipeName: IpcTransportDefaults.ControlPipeName,
                direction: PipeDirection.InOut,
                options: PipeOptions.Asynchronous);

            using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            connectCts.CancelAfter(TimeSpan.FromSeconds(2));
            await client.ConnectAsync(connectCts.Token);

            using var reader = new StreamReader(client, leaveOpen: true);
            using var writer = new StreamWriter(client, leaveOpen: true) { AutoFlush = true };

            var request = new IpcRequestEnvelope<IpcHandshakeRequest>(
                RequestId: Guid.NewGuid().ToString("D"),
                TimestampUtc: DateTimeOffset.UtcNow,
                Method: IpcMethodNames.SystemHandshake,
                Payload: new IpcHandshakeRequest(IpcProtocol.Version, SubscribeEvents: true),
                Client: new IpcClientMetadata(ClientName, ClientVersion));

            var requestJson = JsonSerializer.Serialize(request, IpcJsonSerializer.Options);
            await writer.WriteLineAsync(requestJson);

            var responseLine = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(responseLine))
            {
                return null;
            }

            var response = JsonSerializer.Deserialize<IpcResponseEnvelope<IpcHandshakeResponse>>(responseLine, IpcJsonSerializer.Options);
            if (response is null || !response.Success)
            {
                return null;
            }

            return response.Payload;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public async Task<IpcStartupHealthSnapshotPayload?> TryGetStartupHealthAsync(CancellationToken cancellationToken = default)
    {
        var response = await SendRequestAsync<object, IpcStartupHealthSnapshotPayload>(
            IpcMethodNames.HealthGetStartup,
            new { },
            cancellationToken);

        return response?.Payload;
    }

    public async Task<IpcConnectionSnapshotPayload?> TryGetConnectionSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var response = await SendRequestAsync<object, IpcConnectionSnapshotPayload>(
            IpcMethodNames.ConnectionsGetSnapshot,
            new { },
            cancellationToken);

        return response?.Payload;
    }

    public async Task<IpcSecurityBootstrapSnapshotPayload?> TryGetSecurityBootstrapAsync(CancellationToken cancellationToken = default)
    {
        var response = await SendRequestAsync<object, IpcSecurityBootstrapSnapshotPayload>(
            IpcMethodNames.SecurityGetBootstrap,
            new { },
            cancellationToken);

        return response?.Payload;
    }

    public async Task<IpcApplyDraftConfigResponse?> TryApplyDraftConfigurationAsync(
        UpstreamEndpointConfigurationDocument draftDocument,
        CancellationToken cancellationToken = default)
    {
        var response = await SendRequestAsync<IpcApplyDraftConfigRequest, IpcApplyDraftConfigResponse>(
            IpcMethodNames.ConnectionsApplyDraftConfig,
            new IpcApplyDraftConfigRequest(draftDocument),
            cancellationToken);

        return response?.Payload;
    }

    private async Task<IpcResponseEnvelope<TResponse>?> SendRequestAsync<TRequest, TResponse>(
        string method,
        TRequest payload,
        CancellationToken cancellationToken)
        where TRequest : notnull
    {
        try
        {
            using var client = new NamedPipeClientStream(
                serverName: ".",
                pipeName: IpcTransportDefaults.ControlPipeName,
                direction: PipeDirection.InOut,
                options: PipeOptions.Asynchronous);

            using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            connectCts.CancelAfter(TimeSpan.FromSeconds(2));
            await client.ConnectAsync(connectCts.Token);

            using var reader = new StreamReader(client, leaveOpen: true);
            using var writer = new StreamWriter(client, leaveOpen: true) { AutoFlush = true };

            var request = new IpcRequestEnvelope<object>(
                RequestId: Guid.NewGuid().ToString("D"),
                TimestampUtc: DateTimeOffset.UtcNow,
                Method: method,
                Payload: payload,
                Client: new IpcClientMetadata(ClientName, ClientVersion));

            var requestJson = JsonSerializer.Serialize(request, IpcJsonSerializer.Options);
            await writer.WriteLineAsync(requestJson);

            var responseLine = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(responseLine))
            {
                return null;
            }

            var response = JsonSerializer.Deserialize<IpcResponseEnvelope<TResponse>>(responseLine, IpcJsonSerializer.Options);
            if (response is null || !response.Success)
            {
                return null;
            }

            return response;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}