using UAGateway.Core.Configuration;
using Xunit;

namespace UAGateway.Core.Tests;

public sealed class ConfigurationValidationTests
{
    [Fact]
    public void UpstreamEndpointValidator_FlagsInvalidScheme()
    {
        var document = new UpstreamEndpointConfigurationDocument
        {
            Endpoints =
            [
                new UpstreamEndpointConfiguration
                {
                    Id = "endpoint-1",
                    DisplayName = "PLC-1",
                    EndpointUrl = "https://example.com/opcua",
                    Enabled = true,
                },
            ],
        };

        var issues = UpstreamEndpointConfigurationValidator.Validate(document);

        Assert.Contains(issues, issue =>
            issue.EndpointId == "endpoint-1" &&
            issue.Message.Contains("opc.tcp", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void NamespaceMappingValidator_FlagsUnknownEndpointId()
    {
        var endpoints = new UpstreamEndpointConfigurationDocument
        {
            Endpoints =
            [
                new UpstreamEndpointConfiguration
                {
                    Id = "endpoint-1",
                    DisplayName = "PLC-1",
                    EndpointUrl = "opc.tcp://plc1:4840",
                    Enabled = true,
                },
            ],
        };

        var mappings = new NamespaceMappingConfigurationDocument
        {
            Rules =
            [
                new NamespaceMappingRule
                {
                    EndpointId = "unknown-endpoint",
                    ProjectedName = "Renamed",
                    Enabled = true,
                },
            ],
        };

        var issues = NamespaceMappingConfigurationValidator.Validate(mappings, endpoints);

        Assert.Contains(issues, issue =>
            issue.EndpointId == "unknown-endpoint" &&
            issue.Message.Contains("unknown endpoint", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void NamespaceMappingValidator_AllowsMirrorPlusRenameRules()
    {
        var endpoints = new UpstreamEndpointConfigurationDocument
        {
            Endpoints =
            [
                new UpstreamEndpointConfiguration
                {
                    Id = "endpoint-1",
                    DisplayName = "PLC-1",
                    EndpointUrl = "opc.tcp://plc1:4840",
                    Enabled = true,
                },
                new UpstreamEndpointConfiguration
                {
                    Id = "endpoint-2",
                    DisplayName = "PLC-2",
                    EndpointUrl = "opc.tcp://plc2:4840",
                    Enabled = true,
                },
            ],
        };

        var mappings = new NamespaceMappingConfigurationDocument
        {
            Rules =
            [
                new NamespaceMappingRule
                {
                    EndpointId = "endpoint-1",
                    ProjectedName = "BoilerPLC",
                    Enabled = true,
                },
                new NamespaceMappingRule
                {
                    EndpointId = "endpoint-2",
                    ProjectedName = null,
                    Enabled = true,
                },
            ],
        };

        var issues = NamespaceMappingConfigurationValidator.Validate(mappings, endpoints);

        Assert.Empty(issues);
    }

    [Fact]
    public void UpstreamEndpointValidator_FlagsDuplicateIds_CaseInsensitive()
    {
        var document = new UpstreamEndpointConfigurationDocument
        {
            Endpoints =
            [
                new UpstreamEndpointConfiguration
                {
                    Id = "endpoint-1",
                    DisplayName = "PLC-1",
                    EndpointUrl = "opc.tcp://plc1:4840",
                    Enabled = true,
                },
                new UpstreamEndpointConfiguration
                {
                    Id = "ENDPOINT-1",
                    DisplayName = "PLC-1-duplicate",
                    EndpointUrl = "opc.tcp://plc1-dup:4840",
                    Enabled = true,
                },
            ],
        };

        var issues = UpstreamEndpointConfigurationValidator.Validate(document);

        Assert.Contains(issues, issue =>
            issue.EndpointId == "ENDPOINT-1" &&
            issue.Message.Contains("unique", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void NamespaceMappingValidator_FlagsDuplicateProjectedNames_WhenEnabled()
    {
        var endpoints = new UpstreamEndpointConfigurationDocument
        {
            Endpoints =
            [
                new UpstreamEndpointConfiguration
                {
                    Id = "endpoint-1",
                    DisplayName = "PLC-1",
                    EndpointUrl = "opc.tcp://plc1:4840",
                    Enabled = true,
                },
                new UpstreamEndpointConfiguration
                {
                    Id = "endpoint-2",
                    DisplayName = "PLC-2",
                    EndpointUrl = "opc.tcp://plc2:4840",
                    Enabled = true,
                },
            ],
        };

        var mappings = new NamespaceMappingConfigurationDocument
        {
            Rules =
            [
                new NamespaceMappingRule
                {
                    EndpointId = "endpoint-1",
                    ProjectedName = "SharedName",
                    Enabled = true,
                },
                new NamespaceMappingRule
                {
                    EndpointId = "endpoint-2",
                    ProjectedName = "sharedname",
                    Enabled = true,
                },
            ],
        };

        var issues = NamespaceMappingConfigurationValidator.Validate(mappings, endpoints);

        Assert.Contains(issues, issue =>
            issue.EndpointId == "endpoint-2" &&
            issue.Message.Contains("Projected name", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void NamespaceMappingValidator_IgnoresDuplicateProjectedNames_WhenRuleDisabled()
    {
        var endpoints = new UpstreamEndpointConfigurationDocument
        {
            Endpoints =
            [
                new UpstreamEndpointConfiguration
                {
                    Id = "endpoint-1",
                    DisplayName = "PLC-1",
                    EndpointUrl = "opc.tcp://plc1:4840",
                    Enabled = true,
                },
                new UpstreamEndpointConfiguration
                {
                    Id = "endpoint-2",
                    DisplayName = "PLC-2",
                    EndpointUrl = "opc.tcp://plc2:4840",
                    Enabled = true,
                },
            ],
        };

        var mappings = new NamespaceMappingConfigurationDocument
        {
            Rules =
            [
                new NamespaceMappingRule
                {
                    EndpointId = "endpoint-1",
                    ProjectedName = "SharedName",
                    Enabled = true,
                },
                new NamespaceMappingRule
                {
                    EndpointId = "endpoint-2",
                    ProjectedName = "SharedName",
                    Enabled = false,
                },
            ],
        };

        var issues = NamespaceMappingConfigurationValidator.Validate(mappings, endpoints);

        Assert.DoesNotContain(issues, issue =>
            issue.Message.Contains("Projected name", StringComparison.OrdinalIgnoreCase));
    }
}
