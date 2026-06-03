using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using UAGateway.Core.Ipc;

namespace UAGateway.Service;

internal sealed class IpcEventServerHostedService : BackgroundService
{
    private readonly ILogger<IpcEventServerHostedService> _logger;
    private readonly IpcEventStreamBroker _broker;
    private readonly ConcurrentDictionary<Guid, StreamWriter> _clients = new();

    public IpcEventServerHostedService(
        ILogger<IpcEventServerHostedService> logger,
        IpcEventStreamBroker broker)
    {
        _logger = logger;
        _broker = broker;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var acceptLoop = RunAcceptLoopAsync(stoppingToken);
        var publishLoop = RunPublishLoopAsync(stoppingToken);

        await Task.WhenAll(acceptLoop, publishLoop);
    }

    private async Task RunAcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var server = new NamedPipeServerStream(
                    pipeName: IpcTransportDefaults.EventPipeName,
                    direction: PipeDirection.Out,
                    maxNumberOfServerInstances: NamedPipeServerStream.MaxAllowedServerInstances,
                    transmissionMode: PipeTransmissionMode.Byte,
                    options: PipeOptions.Asynchronous);

                await server.WaitForConnectionAsync(cancellationToken);

                var writer = new StreamWriter(server) { AutoFlush = true };
                var clientId = Guid.NewGuid();

                if (!_clients.TryAdd(clientId, writer))
                {
                    writer.Dispose();
                    continue;
                }

                _logger.LogInformation("IPC event stream client connected: {ClientId}", clientId);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "IPC event stream accept loop error.");
            }
        }
    }

    private async Task RunPublishLoopAsync(CancellationToken cancellationToken)
    {
        await foreach (var evt in _broker.ReadAllAsync(cancellationToken))
        {
            if (_clients.IsEmpty)
            {
                continue;
            }

            var json = JsonSerializer.Serialize(evt, IpcJsonSerializer.Options);
            var disconnected = new List<Guid>();

            foreach (var pair in _clients)
            {
                try
                {
                    await pair.Value.WriteLineAsync(json);
                }
                catch
                {
                    disconnected.Add(pair.Key);
                }
            }

            foreach (var clientId in disconnected)
            {
                RemoveClient(clientId);
            }
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        foreach (var clientId in _clients.Keys)
        {
            RemoveClient(clientId);
        }

        return base.StopAsync(cancellationToken);
    }

    private void RemoveClient(Guid clientId)
    {
        if (_clients.TryRemove(clientId, out var writer))
        {
            try
            {
                writer.Dispose();
            }
            catch
            {
                // Best-effort disposal only.
            }

            _logger.LogInformation("IPC event stream client disconnected: {ClientId}", clientId);
        }
    }
}
