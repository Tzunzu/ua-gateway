using Opc.Ua;
using Opc.Ua.Configuration;
using Opc.Ua.Server;
using UAGateway.Core.Configuration;

namespace UAGateway.Service;

internal sealed class UAGatewayLocalServer : StandardServer
{
    private readonly Action<int>? _onProjectionBuilt;
    private readonly NamespaceMappingConfigurationDocument _mapping;

    public UAGatewayLocalServer(
        NamespaceMappingConfigurationDocument mapping,
        Action<int>? onProjectionBuilt = null)
    {
        _mapping = mapping;
        _onProjectionBuilt = onProjectionBuilt;
    }

    protected override MasterNodeManager CreateMasterNodeManager(IServerInternal server, ApplicationConfiguration configuration)
    {
        var endpoints = UpstreamEndpointConfigurationStore.LoadOrCreateDefault().Endpoints;
        var disabledEndpointIds = _mapping.Rules
            .Where(rule => !rule.Enabled)
            .Select(rule => rule.EndpointId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var projectedEndpointCount = endpoints.Count(endpoint => !disabledEndpointIds.Contains(endpoint.Id));
        _onProjectionBuilt?.Invoke(projectedEndpointCount);

        var projectionNodeManager = new GatewayProjectionNodeManager(server, configuration, _mapping);
        return new MasterNodeManager(server, configuration, null, projectionNodeManager);
    }
}
