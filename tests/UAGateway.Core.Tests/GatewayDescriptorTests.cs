namespace UAGateway.Core.Tests;

public static class GatewayDescriptorTests
{
    public static void CanCreateDescriptor()
    {
        var descriptor = new UAGateway.Core.GatewayDescriptor("UA Gateway", "OPC UA gateway");
        if (descriptor.Name != "UA Gateway")
        {
            throw new InvalidOperationException("Descriptor name mismatch.");
        }
    }
}
