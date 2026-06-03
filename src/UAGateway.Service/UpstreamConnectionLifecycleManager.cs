using Microsoft.Extensions.Logging;
using Opc.Ua;
using Opc.Ua.Client;
using UAGateway.Core.Configuration;
using UAGateway.Core.Diagnostics;
using UAGateway.Core.Ipc;

namespace UAGateway.Service;

internal sealed class UpstreamConnectionLifecycleManager
{
    private readonly ILogger<UpstreamConnectionLifecycleManager> _logger;
    private readonly ITelemetryContext _telemetryContext;
    private readonly IpcEventStreamBroker _eventStreamBroker;
    private readonly object _sync = new();
    private readonly Dictionary<string, EndpointRuntimeState> _runtimeByEndpointId = new(StringComparer.OrdinalIgnoreCase);
    private ApplicationConfiguration? _applicationConfiguration;

    public UpstreamConnectionLifecycleManager(
        ILogger<UpstreamConnectionLifecycleManager> logger,
        IpcEventStreamBroker eventStreamBroker)
    {
        _logger = logger;
        _telemetryContext = DefaultTelemetry.Create(_ => { });
        _eventStreamBroker = eventStreamBroker;
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

    public ConnectionLifecycleDiagnosticsSnapshot GetSnapshot(DateTimeOffset nowUtc)
    {
        lock (_sync)
        {
            var allStates = _runtimeByEndpointId.Values.ToList();

            return new ConnectionLifecycleDiagnosticsSnapshot(
                nowUtc,
                allStates.Count(state => state.Enabled),
                allStates.Count(state => state.Enabled && string.Equals(state.State, "Connected", StringComparison.Ordinal)),
                allStates.Count(state => state.Enabled && string.Equals(state.State, "Connecting", StringComparison.Ordinal)),
                allStates.Count(state => state.Enabled && string.Equals(state.State, "Disconnected", StringComparison.Ordinal)),
                allStates.Sum(state => state.FailureCount));
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

            _eventStreamBroker.Publish(
                category: IpcEventCategories.ConnectionLifecycle,
                name: nameof(UAGatewayEventIds.ConnectionLifecycle.UpstreamEndpointsConfigured),
                severity: IpcEventSeverity.Information,
                serviceEventId: UAGatewayEventIds.ConnectionLifecycle.UpstreamEndpointsConfigured,
                message: $"Upstream endpoints configured. Enabled={_runtimeByEndpointId.Values.Count(state => state.Enabled)}");
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
            _eventStreamBroker.Publish(
                category: IpcEventCategories.ConnectionLifecycle,
                name: nameof(UAGatewayEventIds.ConnectionLifecycle.ConnectionAttemptStarted),
                severity: IpcEventSeverity.Information,
                serviceEventId: UAGatewayEventIds.ConnectionLifecycle.ConnectionAttemptStarted,
                message: $"Connection attempt started for {endpoint.EndpointUrl}",
                endpointId: endpoint.EndpointId,
                correlationId: correlationId);
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
                _eventStreamBroker.Publish(
                    category: IpcEventCategories.ConnectionLifecycle,
                    name: nameof(UAGatewayEventIds.ConnectionLifecycle.ConnectionAttemptSucceeded),
                    severity: IpcEventSeverity.Information,
                    serviceEventId: UAGatewayEventIds.ConnectionLifecycle.ConnectionAttemptSucceeded,
                    message: $"Connection attempt succeeded for {endpoint.EndpointUrl}",
                    endpointId: endpoint.EndpointId,
                    correlationId: correlationId);
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

                _eventStreamBroker.Publish(
                    category: IpcEventCategories.ConnectionLifecycle,
                    name: nameof(UAGatewayEventIds.ConnectionLifecycle.ConnectionAttemptFailed),
                    severity: IpcEventSeverity.Warning,
                    serviceEventId: UAGatewayEventIds.ConnectionLifecycle.ConnectionAttemptFailed,
                    message: $"Connection attempt failed for {endpoint.EndpointUrl}",
                    endpointId: endpoint.EndpointId,
                    detail: ex.Message,
                    correlationId: correlationId);
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
        _eventStreamBroker.Publish(
            category: IpcEventCategories.ConnectionLifecycle,
            name: nameof(UAGatewayEventIds.ConnectionLifecycle.ConnectionStateChanged),
            severity: IpcEventSeverity.Information,
            serviceEventId: UAGatewayEventIds.ConnectionLifecycle.ConnectionStateChanged,
            message: $"Connection state changed to {nextState}",
            endpointId: endpoint.EndpointId);
    }

    private static TimeSpan ComputeBackoffDelay(int failureCount)
    {
        var seconds = Math.Min(60, Math.Pow(2, Math.Min(failureCount, 6)));
        return TimeSpan.FromSeconds(seconds);
    }

    private void PublishMetricsSnapshot(DateTimeOffset updatedUtc)
    {
        var snapshot = GetSnapshot(updatedUtc);

        ConnectionLifecycleDiagnosticsStore.Save(snapshot);

        GatewayLogMessages.ConnectionMetricsSnapshotPublished(
            _logger,
            snapshot.EnabledEndpointCount,
            snapshot.ConnectedEndpointCount,
            snapshot.ConnectingEndpointCount,
            snapshot.DisconnectedEndpointCount,
            snapshot.TotalFailureCount);

        _eventStreamBroker.Publish(
            category: IpcEventCategories.ConnectionLifecycle,
            name: nameof(UAGatewayEventIds.ConnectionLifecycle.ConnectionMetricsSnapshotPublished),
            severity: IpcEventSeverity.Information,
            serviceEventId: UAGatewayEventIds.ConnectionLifecycle.ConnectionMetricsSnapshotPublished,
            message: $"Connection metrics snapshot. Enabled={snapshot.EnabledEndpointCount}, Connected={snapshot.ConnectedEndpointCount}, Connecting={snapshot.ConnectingEndpointCount}, Disconnected={snapshot.DisconnectedEndpointCount}, Failures={snapshot.TotalFailureCount}");
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
