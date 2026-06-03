namespace UAGateway.Core.Configuration;

public sealed record UpstreamEndpointConfiguration
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string DisplayName { get; init; } = "New Endpoint";
    public string EndpointUrl { get; init; } = string.Empty;
    public bool Enabled { get; init; } = true;
}

public sealed record UpstreamEndpointConfigurationDocument
{
    public List<UpstreamEndpointConfiguration> Endpoints { get; init; } = [];
}

public sealed record UpstreamEndpointValidationIssue(string EndpointId, string Message);

public static class UpstreamEndpointConfigurationValidator
{
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
        }

        return issues;
    }
}
