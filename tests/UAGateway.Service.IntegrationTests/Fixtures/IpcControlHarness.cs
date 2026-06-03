using Microsoft.Extensions.Logging.Abstractions;
using UAGateway.Service;

namespace UAGateway.Service.IntegrationTests.Fixtures;

internal sealed class IpcControlHarness : IAsyncDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly IpcControlServerHostedService _server;

    private IpcControlHarness(
        string pipeName,
        StartupHealthState startupHealthState,
        CancellationTokenSource cancellationTokenSource,
        IpcControlServerHostedService server)
    {
        PipeName = pipeName;
        StartupHealthState = startupHealthState;
        _cancellationTokenSource = cancellationTokenSource;
        _server = server;
    }

    public string PipeName { get; }

    public StartupHealthState StartupHealthState { get; }

    public static async Task<IpcControlHarness> StartAsync()
    {
        var pipeName = $"ua-gateway-control-it-{Guid.NewGuid():N}";
        var startupHealthState = new StartupHealthState();
        var eventBroker = new IpcEventStreamBroker();
        var connectionManager = new UpstreamConnectionLifecycleManager(
            NullLogger<UpstreamConnectionLifecycleManager>.Instance,
            eventBroker);

        var server = new IpcControlServerHostedService(
            NullLogger<IpcControlServerHostedService>.Instance,
            startupHealthState,
            connectionManager,
            pipeName);

        var cancellationTokenSource = new CancellationTokenSource();
        await server.StartAsync(cancellationTokenSource.Token);

        return new IpcControlHarness(pipeName, startupHealthState, cancellationTokenSource, server);
    }

    public async ValueTask DisposeAsync()
    {
        _cancellationTokenSource.Cancel();
        await _server.StopAsync(CancellationToken.None);
        _cancellationTokenSource.Dispose();
    }
}
