using Microsoft.Extensions.Logging;
using Opc.Ua;

namespace UAGateway.Service;

internal sealed class OpcUaBootstrapper
{
    private readonly ILogger<OpcUaBootstrapper> _logger;

    public OpcUaBootstrapper(ILogger<OpcUaBootstrapper> logger)
    {
        _logger = logger;
    }

    public void Initialize()
    {
        var configApplyCorrelationId = CreateCorrelationId();

        using (_logger.BeginScope("CorrelationId:{CorrelationId}", configApplyCorrelationId))
        {
            GatewayLogMessages.ConfigApplyStarted(_logger, configApplyCorrelationId);

            // This confirms the OPC UA stack package is available and wired into the service.
            var statusCode = StatusCodes.Good;
            GatewayLogMessages.OpcUaBootstrapInitialized(_logger, statusCode);

            GatewayLogMessages.ConfigApplyCompleted(_logger, configApplyCorrelationId);
        }

        var reconnectCorrelationId = CreateCorrelationId();

        using (_logger.BeginScope("CorrelationId:{CorrelationId}", reconnectCorrelationId))
        {
            GatewayLogMessages.ReconnectFlowStarted(_logger, reconnectCorrelationId);
            GatewayLogMessages.ConnectionManagerInitialized(_logger);
            GatewayLogMessages.NoUpstreamEndpointsConfigured(_logger, reconnectCorrelationId);
            GatewayLogMessages.ReconnectFlowIdleNoEndpoints(_logger, reconnectCorrelationId);
        }
    }

    private static string CreateCorrelationId()
    {
        return Guid.NewGuid().ToString("N");
    }
}
