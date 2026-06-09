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
    public void UpstreamEndpointValidator_FlagsInvalidSecurityMode()
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
                    Security = new UpstreamEndpointSecuritySettings
                    {
                        SecurityMode = "InvalidMode",
                        SecurityPolicy = "Basic256Sha256",
                    },
                },
            ],
        };

        var issues = UpstreamEndpointConfigurationValidator.Validate(document);

        Assert.Contains(issues, issue =>
            issue.EndpointId == "endpoint-1" &&
            issue.Message.Contains("Security mode", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void UpstreamEndpointValidator_RequiresCredentialId_ForUsernamePasswordMode()
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
                    Authentication = new UpstreamEndpointAuthenticationSettings
                    {
                        Mode = "UsernamePassword",
                        CredentialId = string.Empty,
                    },
                },
            ],
        };

        var issues = UpstreamEndpointConfigurationValidator.Validate(document);

        Assert.Contains(issues, issue =>
            issue.EndpointId == "endpoint-1" &&
            issue.Message.Contains("Credential id", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void UpstreamEndpointValidator_FlagsRetryDelayBounds()
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
                    Retry = new UpstreamEndpointRetrySettings
                    {
                        Strategy = "Exponential",
                        InitialDelaySeconds = 120,
                        MaxDelaySeconds = 10,
                        SuccessProbeIntervalSeconds = 30,
                        MaxAttempts = 0,
                        ReconnectOnFailure = true,
                    },
                },
            ],
        };

        var issues = UpstreamEndpointConfigurationValidator.Validate(document);

        Assert.Contains(issues, issue =>
            issue.EndpointId == "endpoint-1" &&
            issue.Message.Contains("greater than or equal", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void UpstreamEndpointValidator_FlagsInvalidSubscriptionPublishingInterval()
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
                    Subscription = new UpstreamEndpointSubscriptionSettings
                    {
                        PublishingIntervalMs = 10,
                        SamplingIntervalMs = 1000,
                        QueueSize = 100,
                        MaxItemsPerSubscription = 500,
                    },
                },
            ],
        };

        var issues = UpstreamEndpointConfigurationValidator.Validate(document);

        Assert.Contains(issues, issue =>
            issue.EndpointId == "endpoint-1" &&
            issue.Message.Contains("Publishing interval", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void UpstreamEndpointValidator_FlagsInvalidMaxItemsPerSubscription()
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
                    Subscription = new UpstreamEndpointSubscriptionSettings
                    {
                        PublishingIntervalMs = 1000,
                        SamplingIntervalMs = 1000,
                        QueueSize = 100,
                        MaxItemsPerSubscription = 0,
                    },
                },
            ],
        };

        var issues = UpstreamEndpointConfigurationValidator.Validate(document);

        Assert.Contains(issues, issue =>
            issue.EndpointId == "endpoint-1" &&
            issue.Message.Contains("Max items per subscription", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void UpstreamEndpointValidator_FlagsInvalidLifetimeToKeepAliveRatio()
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
                    Subscription = new UpstreamEndpointSubscriptionSettings
                    {
                        PublishingIntervalMs = 1000,
                        SamplingIntervalMs = 1000,
                        QueueSize = 100,
                        MaxItemsPerSubscription = 500,
                        KeepAliveCount = 20,
                        LifetimeCount = 40,
                    },
                },
            ],
        };

        var issues = UpstreamEndpointConfigurationValidator.Validate(document);

        Assert.Contains(issues, issue =>
            issue.EndpointId == "endpoint-1" &&
            issue.Message.Contains("3x keep alive", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void UpstreamEndpointValidator_FlagsInvalidSubscriptionPriority()
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
                    Subscription = new UpstreamEndpointSubscriptionSettings
                    {
                        PublishingIntervalMs = 1000,
                        SamplingIntervalMs = 1000,
                        QueueSize = 100,
                        MaxItemsPerSubscription = 500,
                        Priority = 999,
                    },
                },
            ],
        };

        var issues = UpstreamEndpointConfigurationValidator.Validate(document);

        Assert.Contains(issues, issue =>
            issue.EndpointId == "endpoint-1" &&
            issue.Message.Contains("priority", StringComparison.OrdinalIgnoreCase));
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

    [Fact]
    public void LocalServerConfigurationValidator_AcceptsValidPort()
    {
        var document = new LocalServerConfigurationDocument
        {
            Host = "localhost",
            Port = 4840,
            EndpointPath = "UAGateway",
        };

        var issues = LocalServerConfigurationValidator.Validate(document);

        Assert.Empty(issues);
    }

    [Fact]
    public void LocalServerConfigurationValidator_FlagsOutOfRangePort()
    {
        var document = new LocalServerConfigurationDocument
        {
            Host = "localhost",
            Port = 70000,
            EndpointPath = "UAGateway",
        };

        var issues = LocalServerConfigurationValidator.Validate(document);

        Assert.Contains(issues, issue =>
            issue.Setting == nameof(LocalServerConfigurationDocument.Port) &&
            issue.Message.Contains("between 1 and 65535", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void LocalServerConfigurationValidator_FlagsInvalidHost()
    {
        var document = new LocalServerConfigurationDocument
        {
            Host = "opc.tcp://localhost",
            Port = 4840,
            EndpointPath = "UAGateway",
        };

        var issues = LocalServerConfigurationValidator.Validate(document);

        Assert.Contains(issues, issue =>
            issue.Setting == nameof(LocalServerConfigurationDocument.Host) &&
            issue.Message.Contains("host or IP", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void LocalServerConfigurationValidator_FlagsMissingEndpointPath()
    {
        var document = new LocalServerConfigurationDocument
        {
            Host = "localhost",
            Port = 4840,
            EndpointPath = " / ",
        };

        var issues = LocalServerConfigurationValidator.Validate(document);

        Assert.Contains(issues, issue =>
            issue.Setting == nameof(LocalServerConfigurationDocument.EndpointPath) &&
            issue.Message.Contains("required", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void LocalServerConfigurationValidator_FlagsInvalidSecurityMode()
    {
        var document = new LocalServerConfigurationDocument
        {
            Host = "localhost",
            Port = 4840,
            EndpointPath = "UAGateway",
            SecurityMode = "InvalidMode",
        };

        var issues = LocalServerConfigurationValidator.Validate(document);

        Assert.Contains(issues, issue =>
            issue.Setting == nameof(LocalServerConfigurationDocument.SecurityMode) &&
            issue.Message.Contains("Security mode", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void LocalServerConfigurationValidator_FlagsMissingUserTokenPolicies()
    {
        var document = new LocalServerConfigurationDocument
        {
            Host = "localhost",
            Port = 4840,
            EndpointPath = "UAGateway",
            AllowAnonymous = false,
            AllowUsernamePassword = false,
        };

        var issues = LocalServerConfigurationValidator.Validate(document);

        Assert.Contains(issues, issue =>
            issue.Message.Contains("At least one user token policy", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void LocalServerConfigurationDocument_BuildBaseAddress_UsesNormalizedPath()
    {
        var document = new LocalServerConfigurationDocument
        {
            Host = "127.0.0.1",
            Port = 5123,
            EndpointPath = "/Plant/Gateway/",
        };

        var baseAddress = document.BuildBaseAddress();

        Assert.Equal("opc.tcp://127.0.0.1:5123/Plant/Gateway", baseAddress);
    }
}
