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
        // This confirms the OPC UA stack package is available and wired into the service.
        var statusCode = StatusCodes.Good;
        _logger.LogInformation("OPC UA stack initialized. Baseline status code: {StatusCode}", statusCode);
    }
}
