using UAGateway.Core.Ipc;
using UAGateway.Service.IntegrationTests.Fixtures;
using Xunit;

namespace UAGateway.Service.IntegrationTests.Ipc;

public sealed class ControlHandshakeIntegrationTests
{
    [Fact]
    public async Task ControlPipe_HandshakeRequest_ReturnsSupportedProtocol()
    {
        await using var harness = await IpcControlHarness.StartAsync();

        var response = await IpcControlClient.SendRequestAsync<IpcHandshakeResponse>(
            harness.PipeName,
            IpcMethodNames.SystemHandshake,
            new IpcHandshakeRequest(IpcProtocol.Version, SubscribeEvents: true),
            CancellationToken.None);

        Assert.NotNull(response);
        Assert.True(response!.Success);
        Assert.NotNull(response.Payload);
        Assert.Equal(IpcProtocol.Version, response.Payload.ProtocolVersion);
        Assert.True(response.Payload.Capabilities.EventStream);
    }
}
