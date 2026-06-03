namespace UAGateway.Core.Tests;

using Xunit;

public sealed class GatewayDescriptorTests
{
    [Fact]
    public void CanCreateDescriptor()
    {
        var descriptor = new UAGateway.Core.GatewayDescriptor("UA Gateway", "OPC UA gateway");

        Assert.Equal("UA Gateway", descriptor.Name);
        Assert.Equal("OPC UA gateway", descriptor.Description);
    }
}
