using System.IO.Pipes;
using System.Text.Json;
using UAGateway.Core.Ipc;

namespace UAGateway.UI.Services;

internal sealed class IpcEventStreamClient
{
    private CancellationTokenSource? _runCts;
    private Task? _runTask;

    public void Start(
        Action<IpcEventEnvelope<IpcServiceEventPayload>> onEvent,
        Action<bool, string>? onConnectionState = null)
    {
        if (_runTask is not null && !_runTask.IsCompleted)
        {
            return;
        }

        _runCts = new CancellationTokenSource();
        _runTask = Task.Run(() => RunAsync(onEvent, onConnectionState, _runCts.Token));
    }

    public async Task StopAsync()
    {
        if (_runCts is null)
        {
            return;
        }

        _runCts.Cancel();

        if (_runTask is not null)
        {
            try
            {
                await _runTask;
            }
            catch (OperationCanceledException)
            {
                // Expected during cancellation.
            }
        }

        _runCts.Dispose();
        _runCts = null;
        _runTask = null;
    }

    private static async Task RunAsync(
        Action<IpcEventEnvelope<IpcServiceEventPayload>> onEvent,
        Action<bool, string>? onConnectionState,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using var client = new NamedPipeClientStream(
                    serverName: ".",
                    pipeName: IpcTransportDefaults.EventPipeName,
                    direction: PipeDirection.In,
                    options: PipeOptions.Asynchronous);

                using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                connectCts.CancelAfter(TimeSpan.FromSeconds(2));
                await client.ConnectAsync(connectCts.Token);

                onConnectionState?.Invoke(true, "Live event stream connected.");

                using var reader = new StreamReader(client);

                while (!cancellationToken.IsCancellationRequested && client.IsConnected)
                {
                    var line = await reader.ReadLineAsync(cancellationToken);
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    var evt = JsonSerializer.Deserialize<IpcEventEnvelope<IpcServiceEventPayload>>(line, IpcJsonSerializer.Options);
                    if (evt is not null)
                    {
                        onEvent(evt);
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (IOException)
            {
                onConnectionState?.Invoke(false, "Live event stream unavailable. Falling back to log tail.");
            }
            catch (JsonException)
            {
                // Ignore malformed events and keep reconnecting/reading.
            }

            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
        }

        onConnectionState?.Invoke(false, "Live event stream stopped.");
    }
}
