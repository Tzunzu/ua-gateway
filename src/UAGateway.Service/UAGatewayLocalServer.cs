using Opc.Ua;
using Opc.Ua.Configuration;
using Opc.Ua.Server;
using UAGateway.Core.Configuration;

namespace UAGateway.Service;

internal sealed class UAGatewayLocalServer : StandardServer
{
    private readonly Action<int>? _onProjectionBuilt;

    public UAGatewayLocalServer(Action<int>? onProjectionBuilt = null)
    {
        _onProjectionBuilt = onProjectionBuilt;
    }

    protected override MasterNodeManager CreateMasterNodeManager(IServerInternal server, ApplicationConfiguration configuration)
    {
        var endpointCount = UpstreamEndpointConfigurationStore.LoadOrCreateDefault().Endpoints.Count;
        _onProjectionBuilt?.Invoke(endpointCount);

        var projectionNodeManager = new GatewayProjectionNodeManager(server, configuration);
        return new MasterNodeManager(server, configuration, null, projectionNodeManager);
    }
}
