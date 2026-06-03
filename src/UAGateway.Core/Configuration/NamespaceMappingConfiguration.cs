namespace UAGateway.Core.Configuration;

public sealed record NamespaceMappingRule
{
    public string EndpointId { get; init; } = string.Empty;
    public string? ProjectedName { get; init; }
    public bool Enabled { get; init; } = true;
}

public sealed record NamespaceMappingConfigurationDocument
{
    public List<NamespaceMappingRule> Rules { get; init; } = [];
}

public sealed record NamespaceMappingValidationIssue(string EndpointId, string Message);

public static class NamespaceMappingConfigurationValidator
{
    public static IReadOnlyList<NamespaceMappingValidationIssue> Validate(
        NamespaceMappingConfigurationDocument mappingDocument,
        UpstreamEndpointConfigurationDocument endpointDocument)
    {
        var issues = new List<NamespaceMappingValidationIssue>();
        var endpointIds = endpointDocument.Endpoints
            .Select(endpoint => endpoint.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var seenRuleEndpointIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenProjectedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var rule in mappingDocument.Rules)
        {
            if (string.IsNullOrWhiteSpace(rule.EndpointId))
            {
                issues.Add(new NamespaceMappingValidationIssue("<missing>", "Rule endpoint id is required."));
                continue;
            }

            if (!seenRuleEndpointIds.Add(rule.EndpointId))
            {
                issues.Add(new NamespaceMappingValidationIssue(rule.EndpointId, "Only one mapping rule is allowed per endpoint."));
            }

            if (!endpointIds.Contains(rule.EndpointId))
            {
                issues.Add(new NamespaceMappingValidationIssue(rule.EndpointId, "Mapping rule references an unknown endpoint id."));
            }

            if (!rule.Enabled)
            {
                continue;
            }

            var projectedName = (rule.ProjectedName ?? string.Empty).Trim();
            if (projectedName.Length == 0)
            {
                continue;
            }

            if (!seenProjectedNames.Add(projectedName))
            {
                issues.Add(new NamespaceMappingValidationIssue(rule.EndpointId, "Projected name must be unique among enabled mapping rules."));
            }
        }

        return issues;
    }
}
