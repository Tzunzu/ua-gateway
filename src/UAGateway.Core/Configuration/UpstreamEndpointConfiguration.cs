namespace UAGateway.Core.Configuration;

public sealed record UpstreamEndpointConfiguration
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string DisplayName { get; init; } = "New Endpoint";
    public string EndpointUrl { get; init; } = string.Empty;
    public bool Enabled { get; init; } = true;
    public UpstreamEndpointSecuritySettings Security { get; init; } = new();
    public UpstreamEndpointAuthenticationSettings Authentication { get; init; } = new();
    public UpstreamEndpointTransportSettings Transport { get; init; } = new();
    public UpstreamEndpointSubscriptionSettings Subscription { get; init; } = new();
    public UpstreamEndpointRetrySettings Retry { get; init; } = new();
}

public sealed record UpstreamEndpointSecuritySettings
{
    public string SecurityMode { get; init; } = "SignAndEncrypt";
    public string SecurityPolicy { get; init; } = "Basic256Sha256";
    public bool AutoAcceptUntrustedCertificates { get; init; }
}

public sealed record UpstreamEndpointAuthenticationSettings
{
    public string Mode { get; init; } = "Anonymous";
    public string CredentialId { get; init; } = string.Empty;
}

public sealed record UpstreamEndpointTransportSettings
{
    public int ConnectionTimeoutMs { get; init; } = 5000;
    public int OperationTimeoutMs { get; init; } = 15000;
    public int SessionTimeoutMs { get; init; } = 60000;
}

public sealed record UpstreamEndpointSubscriptionSettings
{
    public int PublishingIntervalMs { get; init; } = 1000;
    public int SamplingIntervalMs { get; init; } = 1000;
    public int QueueSize { get; init; } = 100;
    public int MaxItemsPerSubscription { get; init; } = 500;
    public int KeepAliveCount { get; init; } = 10;
    public int LifetimeCount { get; init; } = 30;
    public int MaxNotificationsPerPublish { get; init; } = 0;
    public bool PublishingEnabled { get; init; } = true;
    public int Priority { get; init; } = 0;
    public bool DiscardOldest { get; init; } = true;
}

public sealed record UpstreamEndpointRetrySettings
{
    public string Strategy { get; init; } = "Exponential";
    public int InitialDelaySeconds { get; init; } = 2;
    public int MaxDelaySeconds { get; init; } = 60;
    public int SuccessProbeIntervalSeconds { get; init; } = 30;
    public int MaxAttempts { get; init; } = 0;
    public bool ReconnectOnFailure { get; init; } = true;
}

public sealed record UpstreamEndpointConfigurationDocument
{
    public List<UpstreamEndpointConfiguration> Endpoints { get; init; } = [];
}

public sealed record UpstreamEndpointValidationIssue(string EndpointId, string Message);

public static class UpstreamEndpointConfigurationValidator
{
    private static readonly HashSet<string> AllowedSecurityModes = new(StringComparer.OrdinalIgnoreCase)
    {
        "None",
        "Sign",
        "SignAndEncrypt",
    };

    private static readonly HashSet<string> AllowedSecurityPolicies = new(StringComparer.OrdinalIgnoreCase)
    {
        "None",
        "Basic128Rsa15",
        "Basic256",
        "Basic256Sha256",
        "Aes128Sha256RsaOaep",
        "Aes256Sha256RsaPss",
    };

    private static readonly HashSet<string> AllowedAuthenticationModes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Anonymous",
        "UsernamePassword",
    };

    private static readonly HashSet<string> AllowedRetryStrategies = new(StringComparer.OrdinalIgnoreCase)
    {
        "Exponential",
        "Linear",
        "Fixed",
    };

    public static IReadOnlyList<UpstreamEndpointValidationIssue> Validate(UpstreamEndpointConfigurationDocument document)
    {
        var issues = new List<UpstreamEndpointValidationIssue>();
        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var endpoint in document.Endpoints)
        {
            if (string.IsNullOrWhiteSpace(endpoint.Id))
            {
                issues.Add(new UpstreamEndpointValidationIssue("<missing>", "Endpoint id is required."));
            }
            else if (!seenIds.Add(endpoint.Id))
            {
                issues.Add(new UpstreamEndpointValidationIssue(endpoint.Id, "Endpoint id must be unique."));
            }

            if (string.IsNullOrWhiteSpace(endpoint.DisplayName))
            {
                issues.Add(new UpstreamEndpointValidationIssue(endpoint.Id, "Display name is required."));
            }

            if (string.IsNullOrWhiteSpace(endpoint.EndpointUrl))
            {
                issues.Add(new UpstreamEndpointValidationIssue(endpoint.Id, "Endpoint URL is required."));
                continue;
            }

            if (!Uri.TryCreate(endpoint.EndpointUrl, UriKind.Absolute, out var uri))
            {
                issues.Add(new UpstreamEndpointValidationIssue(endpoint.Id, "Endpoint URL must be an absolute URI."));
                continue;
            }

            if (!string.Equals(uri.Scheme, "opc.tcp", StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new UpstreamEndpointValidationIssue(endpoint.Id, "Endpoint URL scheme must be opc.tcp."));
            }

            if (!AllowedSecurityModes.Contains(endpoint.Security.SecurityMode))
            {
                issues.Add(new UpstreamEndpointValidationIssue(
                    endpoint.Id,
                    "Security mode must be one of: None, Sign, SignAndEncrypt."));
            }

            if (!AllowedSecurityPolicies.Contains(endpoint.Security.SecurityPolicy))
            {
                issues.Add(new UpstreamEndpointValidationIssue(
                    endpoint.Id,
                    "Security policy must be one of: None, Basic128Rsa15, Basic256, Basic256Sha256, Aes128Sha256RsaOaep, Aes256Sha256RsaPss."));
            }

            if (!AllowedAuthenticationModes.Contains(endpoint.Authentication.Mode))
            {
                issues.Add(new UpstreamEndpointValidationIssue(
                    endpoint.Id,
                    "Authentication mode must be one of: Anonymous, UsernamePassword."));
            }

            if (string.Equals(endpoint.Authentication.Mode, "UsernamePassword", StringComparison.OrdinalIgnoreCase)
                && string.IsNullOrWhiteSpace(endpoint.Authentication.CredentialId))
            {
                issues.Add(new UpstreamEndpointValidationIssue(
                    endpoint.Id,
                    "Credential id is required when authentication mode is UsernamePassword."));
            }

            if (endpoint.Transport.ConnectionTimeoutMs is < 1000 or > 120000)
            {
                issues.Add(new UpstreamEndpointValidationIssue(
                    endpoint.Id,
                    "Connection timeout must be between 1000 and 120000 ms."));
            }

            if (endpoint.Transport.OperationTimeoutMs is < 1000 or > 300000)
            {
                issues.Add(new UpstreamEndpointValidationIssue(
                    endpoint.Id,
                    "Operation timeout must be between 1000 and 300000 ms."));
            }

            if (endpoint.Transport.SessionTimeoutMs is < 5000 or > 3600000)
            {
                issues.Add(new UpstreamEndpointValidationIssue(
                    endpoint.Id,
                    "Session timeout must be between 5000 and 3600000 ms."));
            }

            if (endpoint.Subscription.PublishingIntervalMs is < 100 or > 60000)
            {
                issues.Add(new UpstreamEndpointValidationIssue(
                    endpoint.Id,
                    "Publishing interval must be between 100 and 60000 ms."));
            }

            if (endpoint.Subscription.SamplingIntervalMs is < 100 or > 60000)
            {
                issues.Add(new UpstreamEndpointValidationIssue(
                    endpoint.Id,
                    "Sampling interval must be between 100 and 60000 ms."));
            }

            if (endpoint.Subscription.QueueSize is < 1 or > 10000)
            {
                issues.Add(new UpstreamEndpointValidationIssue(
                    endpoint.Id,
                    "Queue size must be between 1 and 10000."));
            }

            if (endpoint.Subscription.MaxItemsPerSubscription is < 1 or > 10000)
            {
                issues.Add(new UpstreamEndpointValidationIssue(
                    endpoint.Id,
                    "Max items per subscription must be between 1 and 10000."));
            }

            if (endpoint.Subscription.KeepAliveCount is < 1 or > 10000)
            {
                issues.Add(new UpstreamEndpointValidationIssue(
                    endpoint.Id,
                    "Keep alive count must be between 1 and 10000."));
            }

            if (endpoint.Subscription.LifetimeCount is < 3 or > 30000)
            {
                issues.Add(new UpstreamEndpointValidationIssue(
                    endpoint.Id,
                    "Lifetime count must be between 3 and 30000."));
            }

            if (endpoint.Subscription.LifetimeCount < endpoint.Subscription.KeepAliveCount * 3)
            {
                issues.Add(new UpstreamEndpointValidationIssue(
                    endpoint.Id,
                    "Lifetime count must be at least 3x keep alive count."));
            }

            if (endpoint.Subscription.MaxNotificationsPerPublish is < 0 or > 100000)
            {
                issues.Add(new UpstreamEndpointValidationIssue(
                    endpoint.Id,
                    "Max notifications per publish must be between 0 and 100000 (0 means server default)."));
            }

            if (endpoint.Subscription.Priority is < 0 or > 255)
            {
                issues.Add(new UpstreamEndpointValidationIssue(
                    endpoint.Id,
                    "Subscription priority must be between 0 and 255."));
            }

            if (!AllowedRetryStrategies.Contains(endpoint.Retry.Strategy))
            {
                issues.Add(new UpstreamEndpointValidationIssue(
                    endpoint.Id,
                    "Retry strategy must be one of: Exponential, Linear, Fixed."));
            }

            if (endpoint.Retry.InitialDelaySeconds is < 1 or > 300)
            {
                issues.Add(new UpstreamEndpointValidationIssue(
                    endpoint.Id,
                    "Retry initial delay must be between 1 and 300 seconds."));
            }

            if (endpoint.Retry.MaxDelaySeconds is < 1 or > 900)
            {
                issues.Add(new UpstreamEndpointValidationIssue(
                    endpoint.Id,
                    "Retry max delay must be between 1 and 900 seconds."));
            }

            if (endpoint.Retry.MaxDelaySeconds < endpoint.Retry.InitialDelaySeconds)
            {
                issues.Add(new UpstreamEndpointValidationIssue(
                    endpoint.Id,
                    "Retry max delay must be greater than or equal to retry initial delay."));
            }

            if (endpoint.Retry.SuccessProbeIntervalSeconds is < 5 or > 600)
            {
                issues.Add(new UpstreamEndpointValidationIssue(
                    endpoint.Id,
                    "Success probe interval must be between 5 and 600 seconds."));
            }

            if (endpoint.Retry.MaxAttempts is < 0 or > 1000)
            {
                issues.Add(new UpstreamEndpointValidationIssue(
                    endpoint.Id,
                    "Max attempts must be between 0 and 1000 (0 means unlimited)."));
            }
        }

        return issues;
    }
}
