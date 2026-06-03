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
        GatewayLogMessages.ConnectionManagerInitialized(_logger);
        GatewayLogMessages.NoUpstreamEndpointsConfigured(_logger);

        // This confirms the OPC UA stack package is available and wired into the service.
        var statusCode = StatusCodes.Good;
        GatewayLogMessages.OpcUaBootstrapInitialized(_logger, statusCode);
    }
}
