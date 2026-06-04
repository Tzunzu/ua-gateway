using System.Globalization;

namespace UAGateway.Core.Configuration;

public sealed record LocalServerConfigurationDocument
{
    public string Host { get; init; } = "localhost";
    public int Port { get; init; } = 4840;
    public string EndpointPath { get; init; } = "UAGateway";

    public string BuildBaseAddress()
    {
        var host = (Host ?? string.Empty).Trim();
        var endpointPath = NormalizeEndpointPath(EndpointPath);
        return $"opc.tcp://{host}:{Port}/{endpointPath}";
    }

    public static string NormalizeEndpointPath(string? endpointPath)
    {
        return (endpointPath ?? string.Empty).Trim().Trim('/');
    }
}

public sealed record LocalServerConfigurationValidationIssue(string Setting, string Message);

public static class LocalServerConfigurationValidator
{
    public static IReadOnlyList<LocalServerConfigurationValidationIssue> Validate(LocalServerConfigurationDocument document)
    {
        var issues = new List<LocalServerConfigurationValidationIssue>();

        var host = (document.Host ?? string.Empty).Trim();
        if (host.Length == 0)
        {
            issues.Add(new LocalServerConfigurationValidationIssue(
                nameof(document.Host),
                "Local server host is required."));
        }
        else if (host.Contains("://", StringComparison.Ordinal) ||
                 host.Contains('/', StringComparison.Ordinal) ||
                 host.Contains('\\', StringComparison.Ordinal))
        {
            issues.Add(new LocalServerConfigurationValidationIssue(
                nameof(document.Host),
                "Local server host must be only a host or IP value, without scheme or path segments."));
        }

        if (document.Port is < 1 or > 65535)
        {
            issues.Add(new LocalServerConfigurationValidationIssue(
                nameof(document.Port),
                string.Create(CultureInfo.InvariantCulture, $"Local server port must be between 1 and 65535. Current value: {document.Port}.")));
        }

        var endpointPath = LocalServerConfigurationDocument.NormalizeEndpointPath(document.EndpointPath);
        if (endpointPath.Length == 0)
        {
            issues.Add(new LocalServerConfigurationValidationIssue(
                nameof(document.EndpointPath),
                "Endpoint path is required."));
        }
        else if (endpointPath.Contains('?', StringComparison.Ordinal) ||
                 endpointPath.Contains('#', StringComparison.Ordinal) ||
                 endpointPath.Contains(' ', StringComparison.Ordinal))
        {
            issues.Add(new LocalServerConfigurationValidationIssue(
                nameof(document.EndpointPath),
                "Endpoint path cannot contain spaces, query string markers, or fragments."));
        }

        Uri? uri = null;
        if (issues.Count == 0 &&
            !Uri.TryCreate(document.BuildBaseAddress(), UriKind.Absolute, out uri))
        {
            issues.Add(new LocalServerConfigurationValidationIssue(
                nameof(LocalServerConfigurationDocument),
                "Local server settings do not produce a valid opc.tcp endpoint URI."));
        }
        else if (issues.Count == 0 && uri is not null &&
                 !string.Equals(uri.Scheme, "opc.tcp", StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new LocalServerConfigurationValidationIssue(
                nameof(LocalServerConfigurationDocument),
                "Local server settings must produce an opc.tcp endpoint URI."));
        }

        return issues;
    }
}