namespace UAGateway.Core.Diagnostics;

public static class UAGatewayEventIds
{
    public static class ServiceLifecycle
    {
        public const int WorkerStarting = 1000;
        public const int WorkerStarted = 1001;
        public const int WorkerHeartbeat = 1002;
        public const int OpcUaBootstrapInitialized = 1003;
        public const int StartupHealthStateChanged = 1004;
    }

    public static class ConnectionLifecycle
    {
        public const int ConnectionManagerInitialized = 2000;
        public const int NoUpstreamEndpointsConfigured = 2001;
        public const int ReconnectFlowStarted = 2002;
        public const int ReconnectFlowIdleNoEndpoints = 2003;
        public const int UpstreamEndpointsConfigured = 2004;
        public const int ConnectionAttemptStarted = 2005;
        public const int ConnectionAttemptSucceeded = 2006;
        public const int ConnectionAttemptFailed = 2007;
        public const int ConnectionStateChanged = 2008;
        public const int ConnectionMetricsSnapshotPublished = 2009;
        public const int ReservedStart = 2000;
        public const int ReservedEnd = 2199;
    }

    public static class SecurityAndTrust
    {
        public const int SecurityBootstrapStarted = 3000;
        public const int ApplicationCertificateReady = 3001;
        public const int TrustStoreStateObserved = 3002;
        public const int SecurityBootstrapFailed = 3003;
        public const int TrustPolicyDefaultsApplied = 3004;
        public const int NoTrustedPeersConfigured = 3005;
        public const int UntrustedCertificateRejected = 3006;
        public const int SecurityDiagnosticsPublished = 3007;
        public const int ReservedStart = 3000;
        public const int ReservedEnd = 3199;
    }

    public static class MappingAndConfiguration
    {
        public const int ConfigApplyStarted = 4000;
        public const int ConfigApplyCompleted = 4001;
        public const int OpcUaConfigurationBuildStarted = 4002;
        public const int OpcUaConfigurationValidated = 4003;
        public const int OpcUaConfigurationValidationFailed = 4004;
        public const int UpstreamEndpointConfigurationLoaded = 4005;
        public const int UpstreamEndpointConfigurationValidationFailed = 4006;
        public const int ReservedStart = 4000;
        public const int ReservedEnd = 4299;
    }

    public static class LocalServerEndpoint
    {
        public const int ReservedStart = 5000;
        public const int ReservedEnd = 5299;
    }
}
