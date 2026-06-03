namespace UAGateway.Core.Diagnostics;

public static class UAGatewayEventIds
{
    public static class ServiceLifecycle
    {
        public const int WorkerStarting = 1000;
        public const int WorkerStarted = 1001;
        public const int WorkerHeartbeat = 1002;
        public const int OpcUaBootstrapInitialized = 1003;
    }

    public static class ConnectionLifecycle
    {
        public const int ConnectionManagerInitialized = 2000;
        public const int NoUpstreamEndpointsConfigured = 2001;
        public const int ReconnectFlowStarted = 2002;
        public const int ReconnectFlowIdleNoEndpoints = 2003;
        public const int ReservedStart = 2000;
        public const int ReservedEnd = 2199;
    }

    public static class SecurityAndTrust
    {
        public const int ReservedStart = 3000;
        public const int ReservedEnd = 3199;
    }

    public static class MappingAndConfiguration
    {
        public const int ConfigApplyStarted = 4000;
        public const int ConfigApplyCompleted = 4001;
        public const int ReservedStart = 4000;
        public const int ReservedEnd = 4299;
    }

    public static class LocalServerEndpoint
    {
        public const int ReservedStart = 5000;
        public const int ReservedEnd = 5299;
    }
}
