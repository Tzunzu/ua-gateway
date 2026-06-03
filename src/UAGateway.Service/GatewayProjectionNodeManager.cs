using Opc.Ua;
using Opc.Ua.Server;
using UAGateway.Core.Configuration;

namespace UAGateway.Service;

internal sealed class GatewayProjectionNodeManager : CustomNodeManager2
{
    private readonly IList<UpstreamEndpointConfiguration> _endpoints;

    public GatewayProjectionNodeManager(IServerInternal server, ApplicationConfiguration configuration)
        : base(server, configuration, "urn:uagateway:projection")
    {
        SystemContext.NodeIdFactory = this;
        _endpoints = UpstreamEndpointConfigurationStore.LoadOrCreateDefault().Endpoints;
    }

    public override NodeId New(ISystemContext context, NodeState node)
    {
        return node.NodeId ?? new NodeId(Guid.NewGuid().ToString("N"), NamespaceIndex);
    }

    protected override NodeStateCollection LoadPredefinedNodes(ISystemContext context)
    {
        return [];
    }

    public override void CreateAddressSpace(IDictionary<NodeId, IList<IReference>> externalReferences)
    {
        if (!externalReferences.TryGetValue(ObjectIds.ObjectsFolder, out var references))
        {
            references = [];
            externalReferences[ObjectIds.ObjectsFolder] = references;
        }

        var projectionRoot = CreateFolder(
            parent: null,
            path: "Projection",
            name: "Projection",
            namespaceIndex: NamespaceIndex);

        projectionRoot.AddReference(ReferenceTypeIds.Organizes, true, ObjectIds.ObjectsFolder);
        references.Add(new NodeStateReference(ReferenceTypeIds.Organizes, false, projectionRoot.NodeId));

        AddPredefinedNode(SystemContext, projectionRoot);

        foreach (var endpoint in _endpoints)
        {
            var endpointFolder = CreateFolder(
                projectionRoot,
                endpoint.Id,
                endpoint.DisplayName,
                NamespaceIndex);

            var endpointUrlNode = CreateVariable(endpointFolder, "EndpointUrl", endpoint.EndpointUrl, DataTypeIds.String);
            var endpointEnabledNode = CreateVariable(endpointFolder, "Enabled", endpoint.Enabled, DataTypeIds.Boolean);

            AddPredefinedNode(SystemContext, endpointFolder);
            AddPredefinedNode(SystemContext, endpointUrlNode);
            AddPredefinedNode(SystemContext, endpointEnabledNode);
        }
    }

    private FolderState CreateFolder(NodeState? parent, string path, string name, ushort namespaceIndex)
    {
        var folder = new FolderState(parent)
        {
            SymbolicName = name,
            ReferenceTypeId = ReferenceTypeIds.Organizes,
            TypeDefinitionId = ObjectTypeIds.FolderType,
            NodeId = new NodeId(path, namespaceIndex),
            BrowseName = new QualifiedName(name, namespaceIndex),
            DisplayName = new LocalizedText(name),
            WriteMask = AttributeWriteMask.None,
            UserWriteMask = AttributeWriteMask.None,
            EventNotifier = EventNotifiers.None,
        };

        parent?.AddChild(folder);
        return folder;
    }

    private BaseDataVariableState CreateVariable(NodeState parent, string name, object value, NodeId dataType)
    {
        var variable = new BaseDataVariableState(parent)
        {
            SymbolicName = name,
            ReferenceTypeId = ReferenceTypeIds.HasComponent,
            TypeDefinitionId = VariableTypeIds.BaseDataVariableType,
            NodeId = new NodeId($"{parent.NodeId}/{name}", NamespaceIndex),
            BrowseName = new QualifiedName(name, NamespaceIndex),
            DisplayName = new LocalizedText(name),
            DataType = dataType,
            ValueRank = ValueRanks.Scalar,
            AccessLevel = AccessLevels.CurrentRead,
            UserAccessLevel = AccessLevels.CurrentRead,
            Historizing = false,
            Value = value,
            StatusCode = StatusCodes.Good,
            Timestamp = DateTime.UtcNow,
        };

        parent.AddChild(variable);
        return variable;
    }
}
