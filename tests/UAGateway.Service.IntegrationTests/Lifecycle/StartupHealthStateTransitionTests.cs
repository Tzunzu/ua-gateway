using UAGateway.Core.Health;
using UAGateway.Service;
using Xunit;

namespace UAGateway.Service.IntegrationTests.Lifecycle;

public sealed class StartupHealthStateTransitionTests
{
    [Fact]
    public void Update_TracksHealthyDegradedFaultedTransitions()
    {
        var state = new StartupHealthState();

        var healthy = state.Update(StartupHealthStatus.Healthy, "Startup completed.", "m6-health-1");
        Assert.Equal(StartupHealthStatus.Healthy, healthy.Status);
        Assert.Equal("Startup completed.", healthy.Reason);

        var degraded = state.Update(StartupHealthStatus.Degraded, "Security trust warnings.", "m6-health-2");
        Assert.Equal(StartupHealthStatus.Degraded, degraded.Status);
        Assert.Equal("Security trust warnings.", degraded.Reason);

        var faulted = state.Update(StartupHealthStatus.Faulted, "Certificate bootstrap failed.", "m6-health-3");
        Assert.Equal(StartupHealthStatus.Faulted, faulted.Status);
        Assert.Equal("Certificate bootstrap failed.", faulted.Reason);

        Assert.Equal(StartupHealthStatus.Faulted, state.Current.Status);
        Assert.Equal("m6-health-3", state.Current.CorrelationId);
    }
}
