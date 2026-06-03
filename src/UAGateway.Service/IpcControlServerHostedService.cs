using System.IO.Pipes;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using UAGateway.Core.Diagnostics;
using UAGateway.Core.Ipc;

namespace UAGateway.Service;

internal sealed class IpcControlServerHostedService : BackgroundService
{
    private readonly ILogger<IpcControlServerHostedService> _logger;
    private readonly StartupHealthState _startupHealthState;
    private readonly UpstreamConnectionLifecycleManager _connectionLifecycleManager;
    private readonly string _pipeName;

    public IpcControlServerHostedService(
        ILogger<IpcControlServerHostedService> logger,
        StartupHealthState startupHealthState,
        UpstreamConnectionLifecycleManager connectionLifecycleManager,
        string? pipeName = null)
    {
        _logger = logger;
        _startupHealthState = startupHealthState;
        _connectionLifecycleManager = connectionLifecycleManager;
        _pipeName = string.IsNullOrWhiteSpace(pipeName)
            ? IpcTransportDefaults.ControlPipeName
            : pipeName;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var server = new NamedPipeServerStream(
                    pipeName: _pipeName,
                    direction: PipeDirection.InOut,
                    maxNumberOfServerInstances: NamedPipeServerStream.MaxAllowedServerInstances,
                    transmissionMode: PipeTransmissionMode.Byte,
                    options: PipeOptions.Asynchronous);

                await server.WaitForConnectionAsync(stoppingToken);
                await ProcessClientAsync(server, _startupHealthState, _connectionLifecycleManager, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "IPC control server loop error.");
            }
        }
    }

    private static async Task ProcessClientAsync(
        NamedPipeServerStream server,
        StartupHealthState startupHealthState,
        UpstreamConnectionLifecycleManager connectionLifecycleManager,
        CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(server, leaveOpen: true);
        using var writer = new StreamWriter(server, leaveOpen: true) { AutoFlush = true };

        while (!cancellationToken.IsCancellationRequested && server.IsConnected)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                break;
            }

            var request = JsonSerializer.Deserialize<IpcRequestEnvelope<JsonElement>>(line, IpcJsonSerializer.Options);
            if (request is null)
            {
                continue;
            }

            switch (request.Method)
            {
                case IpcMethodNames.SystemHandshake:
                    await HandleHandshakeAsync(request, writer);
                    break;

                case IpcMethodNames.HealthGetStartup:
                    await HandleGetStartupHealthAsync(request, startupHealthState, writer);
                    break;

                case IpcMethodNames.ConnectionsGetSnapshot:
                    await HandleGetConnectionSnapshotAsync(request, connectionLifecycleManager, writer);
                    break;

                case IpcMethodNames.SecurityGetBootstrap:
                    await HandleGetSecurityBootstrapAsync(request, writer);
                    break;

                default:
                    await WriteResponseAsync(
                        writer,
                        new IpcResponseEnvelope<object?>(
                            RequestId: request.RequestId,
                            TimestampUtc: DateTimeOffset.UtcNow,
                            Success: false,
                            ErrorCode: IpcErrorCodes.MethodNotFound,
                            Message: $"Unknown IPC method: {request.Method}",
                            Payload: null));
                    break;
            }
        }
    }

    private static async Task HandleGetStartupHealthAsync(
        IpcRequestEnvelope<JsonElement> request,
        StartupHealthState startupHealthState,
        StreamWriter writer)
    {
        var payload = new IpcStartupHealthSnapshotPayload(
            Available: true,
            Snapshot: startupHealthState.Current);

        await WriteResponseAsync(
            writer,
            new IpcResponseEnvelope<IpcStartupHealthSnapshotPayload>(
                RequestId: request.RequestId,
                TimestampUtc: DateTimeOffset.UtcNow,
                Success: true,
                ErrorCode: null,
                Message: null,
                Payload: payload));
    }

    private static async Task HandleGetConnectionSnapshotAsync(
        IpcRequestEnvelope<JsonElement> request,
        UpstreamConnectionLifecycleManager connectionLifecycleManager,
        StreamWriter writer)
    {
        var payload = new IpcConnectionSnapshotPayload(
            Available: true,
            Snapshot: connectionLifecycleManager.GetSnapshot(DateTimeOffset.UtcNow));

        await WriteResponseAsync(
            writer,
            new IpcResponseEnvelope<IpcConnectionSnapshotPayload>(
                RequestId: request.RequestId,
                TimestampUtc: DateTimeOffset.UtcNow,
                Success: true,
                ErrorCode: null,
                Message: null,
                Payload: payload));
    }

    private static async Task HandleGetSecurityBootstrapAsync(
        IpcRequestEnvelope<JsonElement> request,
        StreamWriter writer)
    {
        var hasSnapshot = SecurityBootstrapDiagnosticsStore.TryLoad(out var snapshot);

        var payload = new IpcSecurityBootstrapSnapshotPayload(
            Available: hasSnapshot && snapshot is not null,
            Snapshot: snapshot);

        await WriteResponseAsync(
            writer,
            new IpcResponseEnvelope<IpcSecurityBootstrapSnapshotPayload>(
                RequestId: request.RequestId,
                TimestampUtc: DateTimeOffset.UtcNow,
                Success: true,
                ErrorCode: null,
                Message: null,
                Payload: payload));
    }

    private static async Task HandleHandshakeAsync(IpcRequestEnvelope<JsonElement> request, StreamWriter writer)
    {
        var handshakeRequest = request.Payload.Deserialize<IpcHandshakeRequest>(IpcJsonSerializer.Options)
            ?? new IpcHandshakeRequest(IpcProtocol.Version, SubscribeEvents: true);

        if (!string.Equals(handshakeRequest.RequestedProtocolVersion, IpcProtocol.Version, StringComparison.Ordinal))
        {
            await WriteResponseAsync(
                writer,
                new IpcResponseEnvelope<object?>(
                    RequestId: request.RequestId,
                    TimestampUtc: DateTimeOffset.UtcNow,
                    Success: false,
                    ErrorCode: IpcErrorCodes.ProtocolVersionUnsupported,
                    Message: $"Requested protocol {handshakeRequest.RequestedProtocolVersion} is not supported. Service supports {IpcProtocol.Version}.",
                    Payload: null));
            return;
        }

        var serviceVersion = typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown";
        var payload = new IpcHandshakeResponse(
            ProtocolVersion: IpcProtocol.Version,
            ServiceVersion: serviceVersion,
            Capabilities: new IpcCapabilitySet(
                EventStream: true,
                ApplyConfig: true,
                LiveLogEvents: true,
                SecurityActions: false));

        await WriteResponseAsync(
            writer,
            new IpcResponseEnvelope<IpcHandshakeResponse>(
                RequestId: request.RequestId,
                TimestampUtc: DateTimeOffset.UtcNow,
                Success: true,
                ErrorCode: null,
                Message: null,
                Payload: payload));
    }

    private static async Task WriteResponseAsync<TPayload>(StreamWriter writer, IpcResponseEnvelope<TPayload> response)
    {
        var json = JsonSerializer.Serialize(response, IpcJsonSerializer.Options);
        await writer.WriteLineAsync(json);
    }
}