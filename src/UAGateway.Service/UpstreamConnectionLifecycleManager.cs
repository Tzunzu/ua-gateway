using Microsoft.Extensions.Logging;
using Opc.Ua;
using Opc.Ua.Client;
using System.Text;
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
                    _runtimeByEndpointId[endpoint.Id] = EndpointRuntimeState.Create(endpoint);
                    continue;
                }

                runtime.EndpointUrl = endpoint.EndpointUrl;
                runtime.Enabled = endpoint.Enabled;
                runtime.SecurityMode = endpoint.Security.SecurityMode;
                runtime.SecurityPolicy = endpoint.Security.SecurityPolicy;
                runtime.AutoAcceptUntrustedCertificates = endpoint.Security.AutoAcceptUntrustedCertificates;
                runtime.AuthenticationMode = endpoint.Authentication.Mode;
                runtime.CredentialId = endpoint.Authentication.CredentialId;
                runtime.ConnectionTimeoutMs = endpoint.Transport.ConnectionTimeoutMs;
                runtime.OperationTimeoutMs = endpoint.Transport.OperationTimeoutMs;
                runtime.SessionTimeoutMs = endpoint.Transport.SessionTimeoutMs;
                runtime.PublishingIntervalMs = endpoint.Subscription.PublishingIntervalMs;
                runtime.SamplingIntervalMs = endpoint.Subscription.SamplingIntervalMs;
                runtime.QueueSize = endpoint.Subscription.QueueSize;
                runtime.MaxItemsPerSubscription = endpoint.Subscription.MaxItemsPerSubscription;
                runtime.KeepAliveCount = endpoint.Subscription.KeepAliveCount;
                runtime.LifetimeCount = endpoint.Subscription.LifetimeCount;
                runtime.MaxNotificationsPerPublish = endpoint.Subscription.MaxNotificationsPerPublish;
                runtime.PublishingEnabled = endpoint.Subscription.PublishingEnabled;
                runtime.Priority = endpoint.Subscription.Priority;
                runtime.DiscardOldest = endpoint.Subscription.DiscardOldest;
                runtime.RetryStrategy = endpoint.Retry.Strategy;
                runtime.InitialRetryDelaySeconds = endpoint.Retry.InitialDelaySeconds;
                runtime.MaxRetryDelaySeconds = endpoint.Retry.MaxDelaySeconds;
                runtime.SuccessProbeIntervalSeconds = endpoint.Retry.SuccessProbeIntervalSeconds;
                runtime.MaxAttempts = endpoint.Retry.MaxAttempts;
                runtime.ReconnectOnFailure = true;
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
                var originalAutoAccept = _applicationConfiguration.SecurityConfiguration.AutoAcceptUntrustedCertificates;

                try
                {
                    _applicationConfiguration.SecurityConfiguration.AutoAcceptUntrustedCertificates = endpoint.AutoAcceptUntrustedCertificates;
                    ProbeEndpoint(endpoint, _applicationConfiguration);
                }
                finally
                {
                    _applicationConfiguration.SecurityConfiguration.AutoAcceptUntrustedCertificates = originalAutoAccept;
                }

                endpoint.FailureCount = 0;
                endpoint.NextAttemptUtc = nowUtc.AddSeconds(endpoint.SuccessProbeIntervalSeconds);

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

                if (endpoint.MaxAttempts > 0 && endpoint.FailureCount >= endpoint.MaxAttempts)
                {
                    endpoint.NextAttemptUtc = DateTimeOffset.MaxValue;
                }
                else
                {
                    var retryDelay = ComputeBackoffDelay(
                        endpoint.RetryStrategy,
                        endpoint.FailureCount,
                        endpoint.InitialRetryDelaySeconds,
                        endpoint.MaxRetryDelaySeconds);
                    endpoint.NextAttemptUtc = nowUtc.Add(retryDelay);
                }

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

    private void ProbeEndpoint(EndpointRuntimeState endpoint, ApplicationConfiguration applicationConfiguration)
    {
        var selectedEndpoint = CoreClientUtils
            .SelectEndpointAsync(
                applicationConfiguration,
                endpoint.EndpointUrl,
                !string.Equals(endpoint.SecurityMode, "None", StringComparison.OrdinalIgnoreCase),
                endpoint.ConnectionTimeoutMs,
                _telemetryContext,
                CancellationToken.None)
            .GetAwaiter()
            .GetResult();

        if (selectedEndpoint is null)
        {
            throw new InvalidOperationException("Endpoint discovery returned no endpoint description.");
        }

        ValidateEndpointSecurity(endpoint, selectedEndpoint);

        var endpointConfiguration = EndpointConfiguration.Create(applicationConfiguration);
        endpointConfiguration.OperationTimeout = endpoint.OperationTimeoutMs;

        var configuredEndpoint = new ConfiguredEndpoint(null, selectedEndpoint, endpointConfiguration);

        var userIdentity = ResolveUserIdentity(endpoint);

        var sessionFactory = new DefaultSessionFactory(_telemetryContext);
        var session = sessionFactory
            .CreateAsync(
                applicationConfiguration,
                configuredEndpoint,
                false,
                false,
                $"UAGatewayProbe-{endpoint.EndpointId}",
                (uint)endpoint.SessionTimeoutMs,
                userIdentity,
                null,
                CancellationToken.None)
            .GetAwaiter()
            .GetResult();

        try
        {
            // Session creation itself validates endpoint reachability and authentication.
        }
        finally
        {
            session.CloseAsync(0, false, CancellationToken.None).GetAwaiter().GetResult();
        }
    }

    private static IUserIdentity ResolveUserIdentity(EndpointRuntimeState endpoint)
    {
        if (string.Equals(endpoint.AuthenticationMode, "Anonymous", StringComparison.OrdinalIgnoreCase))
        {
            return new UserIdentity();
        }

        if (string.Equals(endpoint.AuthenticationMode, "UsernamePassword", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(endpoint.CredentialId))
            {
                throw new InvalidOperationException("UsernamePassword mode requires a credential id.");
            }

            var credential = UpstreamEndpointCredentialStore.TryLoadUsernamePassword(endpoint.CredentialId);
            if (credential is null)
            {
                throw new InvalidOperationException($"Credential '{endpoint.CredentialId}' was not found or could not be decrypted.");
            }

            return new UserIdentity(credential.Username, Encoding.UTF8.GetBytes(credential.Password));
        }

        throw new InvalidOperationException($"Authentication mode '{endpoint.AuthenticationMode}' is not supported.");
    }

    private static void ValidateEndpointSecurity(EndpointRuntimeState endpoint, EndpointDescription selectedEndpoint)
    {
        var expectedMode = endpoint.SecurityMode;
        if (!string.IsNullOrWhiteSpace(expectedMode)
            && !string.Equals(expectedMode, selectedEndpoint.SecurityMode.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Endpoint security mode mismatch. Expected {expectedMode}, discovered {selectedEndpoint.SecurityMode}.");
        }

        var expectedPolicyUri = ToSecurityPolicyUri(endpoint.SecurityPolicy);
        if (!string.IsNullOrWhiteSpace(expectedPolicyUri)
            && !string.Equals(expectedPolicyUri, selectedEndpoint.SecurityPolicyUri, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Endpoint security policy mismatch. Expected {endpoint.SecurityPolicy}, discovered {selectedEndpoint.SecurityPolicyUri}.");
        }
    }

    private static string? ToSecurityPolicyUri(string policy)
    {
        return policy switch
        {
            "None" => SecurityPolicies.None,
            "Basic128Rsa15" => SecurityPolicies.Basic128Rsa15,
            "Basic256" => SecurityPolicies.Basic256,
            "Basic256Sha256" => SecurityPolicies.Basic256Sha256,
            "Aes128Sha256RsaOaep" => "http://opcfoundation.org/UA/SecurityPolicy#Aes128_Sha256_RsaOaep",
            "Aes256Sha256RsaPss" => "http://opcfoundation.org/UA/SecurityPolicy#Aes256_Sha256_RsaPss",
            _ => null,
        };
    }

    private static TimeSpan ComputeBackoffDelay(string strategy, int failureCount, int initialDelaySeconds, int maxDelaySeconds)
    {
        var normalizedFailure = Math.Max(1, failureCount);

        double seconds = strategy switch
        {
            "Fixed" => initialDelaySeconds,
            "Linear" => initialDelaySeconds * normalizedFailure,
            _ => initialDelaySeconds * Math.Pow(2, Math.Min(normalizedFailure - 1, 10)),
        };

        seconds = Math.Min(maxDelaySeconds, seconds);
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
        public static EndpointRuntimeState Create(UpstreamEndpointConfiguration endpoint)
        {
            return new EndpointRuntimeState(endpoint.Id, endpoint.EndpointUrl, endpoint.Enabled)
            {
                SecurityMode = endpoint.Security.SecurityMode,
                SecurityPolicy = endpoint.Security.SecurityPolicy,
                AutoAcceptUntrustedCertificates = endpoint.Security.AutoAcceptUntrustedCertificates,
                AuthenticationMode = endpoint.Authentication.Mode,
                CredentialId = endpoint.Authentication.CredentialId,
                ConnectionTimeoutMs = endpoint.Transport.ConnectionTimeoutMs,
                OperationTimeoutMs = endpoint.Transport.OperationTimeoutMs,
                SessionTimeoutMs = endpoint.Transport.SessionTimeoutMs,
                PublishingIntervalMs = endpoint.Subscription.PublishingIntervalMs,
                SamplingIntervalMs = endpoint.Subscription.SamplingIntervalMs,
                QueueSize = endpoint.Subscription.QueueSize,
                MaxItemsPerSubscription = endpoint.Subscription.MaxItemsPerSubscription,
                KeepAliveCount = endpoint.Subscription.KeepAliveCount,
                LifetimeCount = endpoint.Subscription.LifetimeCount,
                MaxNotificationsPerPublish = endpoint.Subscription.MaxNotificationsPerPublish,
                PublishingEnabled = endpoint.Subscription.PublishingEnabled,
                Priority = endpoint.Subscription.Priority,
                DiscardOldest = endpoint.Subscription.DiscardOldest,
                RetryStrategy = endpoint.Retry.Strategy,
                InitialRetryDelaySeconds = endpoint.Retry.InitialDelaySeconds,
                MaxRetryDelaySeconds = endpoint.Retry.MaxDelaySeconds,
                SuccessProbeIntervalSeconds = endpoint.Retry.SuccessProbeIntervalSeconds,
                MaxAttempts = endpoint.Retry.MaxAttempts,
                ReconnectOnFailure = endpoint.Retry.ReconnectOnFailure,
            };
        }

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
        public string SecurityMode { get; set; } = "SignAndEncrypt";
        public string SecurityPolicy { get; set; } = "Basic256Sha256";
        public bool AutoAcceptUntrustedCertificates { get; set; }
        public string AuthenticationMode { get; set; } = "Anonymous";
        public string CredentialId { get; set; } = string.Empty;
        public int ConnectionTimeoutMs { get; set; } = 5000;
        public int OperationTimeoutMs { get; set; } = 15000;
        public int SessionTimeoutMs { get; set; } = 60000;
        public int PublishingIntervalMs { get; set; } = 1000;
        public int SamplingIntervalMs { get; set; } = 1000;
        public int QueueSize { get; set; } = 100;
        public int MaxItemsPerSubscription { get; set; } = 500;
        public int KeepAliveCount { get; set; } = 10;
        public int LifetimeCount { get; set; } = 30;
        public int MaxNotificationsPerPublish { get; set; }
        public bool PublishingEnabled { get; set; } = true;
        public int Priority { get; set; }
        public bool DiscardOldest { get; set; } = true;
        public string RetryStrategy { get; set; } = "Exponential";
        public int InitialRetryDelaySeconds { get; set; } = 2;
        public int MaxRetryDelaySeconds { get; set; } = 60;
        public int SuccessProbeIntervalSeconds { get; set; } = 30;
        public int MaxAttempts { get; set; }
        public bool ReconnectOnFailure { get; set; } = true;
    }
}
