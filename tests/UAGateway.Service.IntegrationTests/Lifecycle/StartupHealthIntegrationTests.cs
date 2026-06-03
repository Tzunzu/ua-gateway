using UAGateway.Core.Health;
using UAGateway.Core.Ipc;
using UAGateway.Service.IntegrationTests.Fixtures;
using Xunit;

namespace UAGateway.Service.IntegrationTests.Lifecycle;

public sealed class StartupHealthIntegrationTests
{
    [Fact]
    public async Task HealthGetStartup_WhenStateIsDegraded_ReturnsDegradedSnapshot()
    {
        await using var harness = await IpcControlHarness.StartAsync();
        harness.StartupHealthState.Update(StartupHealthStatus.Degraded, "Integration degraded test", "it-degraded-1");

        var response = await IpcControlClient.SendRequestAsync<IpcStartupHealthSnapshotPayload>(
            harness.PipeName,
            IpcMethodNames.HealthGetStartup,
            new { },
            CancellationToken.None);

        Assert.NotNull(response);
        Assert.True(response!.Success);
        Assert.True(response.Payload.Available);
        Assert.NotNull(response.Payload.Snapshot);
        Assert.Equal(StartupHealthStatus.Degraded, response.Payload.Snapshot!.Status);
        Assert.Equal("Integration degraded test", response.Payload.Snapshot.Reason);
        Assert.Equal("it-degraded-1", response.Payload.Snapshot.CorrelationId);
    }
}
