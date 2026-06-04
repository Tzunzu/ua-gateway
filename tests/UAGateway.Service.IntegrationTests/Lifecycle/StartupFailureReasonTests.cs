using UAGateway.Service;
using Xunit;

namespace UAGateway.Service.IntegrationTests.Lifecycle;

public sealed class StartupFailureReasonTests
{
    [Fact]
    public void BuildStartupFailureReason_WhenUpstreamJsonInvalid_ReturnsDeterministicOperatorMessage()
    {
        var reason = OpcUaBootstrapper.BuildStartupFailureReason(
            new InvalidOperationException("Upstream endpoint configuration file contains invalid JSON."));

        Assert.Equal(
            "Startup failed: Upstream endpoint configuration JSON is malformed. Fix config/upstream-endpoints.json and restart.",
            reason);
    }

    [Fact]
    public void BuildStartupFailureReason_WhenCertificateBootstrapFails_ReturnsDeterministicOperatorMessage()
    {
        var reason = OpcUaBootstrapper.BuildStartupFailureReason(
            new InvalidOperationException("OPC UA certificate bootstrap failed at startup. Check logs for event ID 3003 and error details."));

        Assert.Equal(
            "Startup failed: Certificate bootstrap failed. Verify application certificate and trust stores under %ProgramData%\\UA Gateway\\pki, then restart.",
            reason);
    }

    [Fact]
    public void BuildStartupFailureReason_WhenUnknownError_PreservesUnderlyingMessage()
    {
        var reason = OpcUaBootstrapper.BuildStartupFailureReason(
            new InvalidOperationException("unexpected bootstrap exception"));

        Assert.Equal("Startup failed: unexpected bootstrap exception", reason);
    }
}
