using Microsoft.Extensions.Logging;
using Opc.Ua;
using Opc.Ua.Client;
using UAGateway.Core.Configuration;
using UAGateway.Core.Diagnostics;

namespace UAGateway.Service;

internal sealed class UpstreamConnectionLifecycleManager
{
    private readonly ILogger<UpstreamConnectionLifecycleManager> _logger;
    private readonly ITelemetryContext _telemetryContext;
    private readonly object _sync = new();
    private readonly Dictionary<string, EndpointRuntimeState> _runtimeByEndpointId = new(StringComparer.OrdinalIgnoreCase);
    private ApplicationConfiguration? _applicationConfiguration;

    public UpstreamConnectionLifecycleManager(ILogger<UpstreamConnectionLifecycleManager> logger)
    {
        _logger = logger;
        _telemetryContext = DefaultTelemetry.Create(_ => { });
    }

    public int EnabledEndpointCount
    {
        get
        {
            lock (_sync)
            {
                return _runtimeByEndpointId.Values.Count(state => state.Enabled);
            }
        }
    }

    public void SetApplicationConfiguration(ApplicationConfiguration applicationConfiguration)
    {
        _applicationConfiguration = applicationConfiguration;
    }

    public void ApplyConfiguration(UpstreamEndpointConfigurationDocument document)
    {
        lock (_sync)
        {
            var configuredIds = document.Endpoints
                .Select(endpoint => endpoint.Id)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var orphanedIds = _runtimeByEndpointId.Keys.Where(id => !configuredIds.Contains(id)).ToList();
            foreach (var orphanedId in orphanedIds)
            {
                _runtimeByEndpointId.Remove(orphanedId);
            }

            foreach (var endpoint in document.Endpoints)
            {
                if (!_runtimeByEndpointId.TryGetValue(endpoint.Id, out var runtime))
                {
                    _runtimeByEndpointId[endpoint.Id] = new EndpointRuntimeState(endpoint.Id, endpoint.EndpointUrl, endpoint.Enabled);
                    continue;
                }

                runtime.EndpointUrl = endpoint.EndpointUrl;
                runtime.Enabled = endpoint.Enabled;
            }
        }
    }

    public void RunIteration(DateTimeOffset nowUtc)
    {
        List<EndpointRuntimeState> dueEndpoints;

        lock (_sync)
        {
            dueEndpoints = _runtimeByEndpointId.Values
                .Where(endpoint => endpoint.Enabled && endpoint.NextAttemptUtc <= nowUtc)
                .ToList();
        }

        foreach (var endpoint in dueEndpoints)
        {
            if (_applicationConfiguration is null)
            {
                continue;
            }

            var correlationId = Guid.NewGuid().ToString("N");
            GatewayLogMessages.ConnectionAttemptStarted(_logger, endpoint.EndpointId, endpoint.EndpointUrl, correlationId);
            UpdateState(endpoint, "Connecting");

            try
            {
                _ = CoreClientUtils
                    .SelectEndpointAsync(_applicationConfiguration, endpoint.EndpointUrl, true, 5000, _telemetryContext, CancellationToken.None)
                    .GetAwaiter()
                    .GetResult();

                endpoint.FailureCount = 0;
                endpoint.NextAttemptUtc = nowUtc.AddSeconds(30);

                UpdateState(endpoint, "Connected");
                GatewayLogMessages.ConnectionAttemptSucceeded(_logger, endpoint.EndpointId, endpoint.EndpointUrl);
            }
            catch (Exception ex)
            {
                endpoint.FailureCount++;
                var retryDelay = ComputeBackoffDelay(endpoint.FailureCount);
                endpoint.NextAttemptUtc = nowUtc.Add(retryDelay);

                UpdateState(endpoint, "Disconnected");
                GatewayLogMessages.ConnectionAttemptFailed(
                    _logger,
                    endpoint.EndpointId,
                    endpoint.EndpointUrl,
                    endpoint.FailureCount,
                    endpoint.NextAttemptUtc,
                    ex.Message);
            }
        }

        PublishMetricsSnapshot(nowUtc);
    }

    private void UpdateState(EndpointRuntimeState endpoint, string nextState)
    {
        if (string.Equals(endpoint.State, nextState, StringComparison.Ordinal))
        {
            return;
        }

        endpoint.State = nextState;
        GatewayLogMessages.ConnectionStateChanged(_logger, endpoint.EndpointId, nextState);
    }

    private static TimeSpan ComputeBackoffDelay(int failureCount)
    {
        var seconds = Math.Min(60, Math.Pow(2, Math.Min(failureCount, 6)));
        return TimeSpan.FromSeconds(seconds);
    }

    private void PublishMetricsSnapshot(DateTimeOffset updatedUtc)
    {
        int enabledCount;
        int connectedCount;
        int connectingCount;
        int disconnectedCount;
        int totalFailures;

        lock (_sync)
        {
            var allStates = _runtimeByEndpointId.Values.ToList();
            enabledCount = allStates.Count(state => state.Enabled);
            connectedCount = allStates.Count(state => state.Enabled && string.Equals(state.State, "Connected", StringComparison.Ordinal));
            connectingCount = allStates.Count(state => state.Enabled && string.Equals(state.State, "Connecting", StringComparison.Ordinal));
            disconnectedCount = allStates.Count(state => state.Enabled && string.Equals(state.State, "Disconnected", StringComparison.Ordinal));
            totalFailures = allStates.Sum(state => state.FailureCount);
        }

        var snapshot = new ConnectionLifecycleDiagnosticsSnapshot(
            updatedUtc,
            enabledCount,
            connectedCount,
            connectingCount,
            disconnectedCount,
            totalFailures);

        ConnectionLifecycleDiagnosticsStore.Save(snapshot);

        GatewayLogMessages.ConnectionMetricsSnapshotPublished(
            _logger,
            enabledCount,
            connectedCount,
            connectingCount,
            disconnectedCount,
            totalFailures);
    }

    private sealed class EndpointRuntimeState
    {
        public EndpointRuntimeState(string endpointId, string endpointUrl, bool enabled)
        {
            EndpointId = endpointId;
            EndpointUrl = endpointUrl;
            Enabled = enabled;
            State = "Disconnected";
            NextAttemptUtc = DateTimeOffset.UtcNow;
        }

        public string EndpointId { get; }
        public string EndpointUrl { get; set; }
        public bool Enabled { get; set; }
        public string State { get; set; }
        public int FailureCount { get; set; }
        public DateTimeOffset NextAttemptUtc { get; set; }
    }
}
