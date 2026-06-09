using System.Globalization;

namespace UAGateway.Core.Configuration;

public sealed record LocalServerConfigurationDocument
{
    public string Host { get; init; } = "localhost";
    public int Port { get; init; } = 4840;
    public string EndpointPath { get; init; } = "UAGateway";
    public string ApplicationName { get; init; } = "UA Gateway";
    public string ProductUri { get; init; } = "urn:uagateway:service";
    public string SecurityMode { get; init; } = "SignAndEncrypt";
    public string SecurityPolicy { get; init; } = "Basic256Sha256";
    public bool AllowAnonymous { get; init; } = true;
    public bool AllowUsernamePassword { get; init; } = true;

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
    private static readonly string[] SupportedSecurityModes =
    [
        "None",
        "Sign",
        "SignAndEncrypt",
    ];

    private static readonly string[] SupportedSecurityPolicies =
    [
        "None",
        "Basic128Rsa15",
        "Basic256",
        "Basic256Sha256",
        "Aes128Sha256RsaOaep",
        "Aes256Sha256RsaPss",
    ];

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

        var applicationName = (document.ApplicationName ?? string.Empty).Trim();
        if (applicationName.Length == 0)
        {
            issues.Add(new LocalServerConfigurationValidationIssue(
                nameof(document.ApplicationName),
                "Application name is required."));
        }

        var productUri = (document.ProductUri ?? string.Empty).Trim();
        if (productUri.Length == 0)
        {
            issues.Add(new LocalServerConfigurationValidationIssue(
                nameof(document.ProductUri),
                "Product URI is required."));
        }

        if (!ContainsOrdinalIgnoreCase(SupportedSecurityModes, document.SecurityMode))
        {
            issues.Add(new LocalServerConfigurationValidationIssue(
                nameof(document.SecurityMode),
                "Security mode must be one of: None, Sign, SignAndEncrypt."));
        }

        if (!ContainsOrdinalIgnoreCase(SupportedSecurityPolicies, document.SecurityPolicy))
        {
            issues.Add(new LocalServerConfigurationValidationIssue(
                nameof(document.SecurityPolicy),
                "Security policy must be one of the supported OPC UA policies."));
        }

        if (string.Equals(document.SecurityMode, "None", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(document.SecurityPolicy, "None", StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new LocalServerConfigurationValidationIssue(
                nameof(document.SecurityPolicy),
                "Security policy must be None when security mode is None."));
        }

        if (!document.AllowAnonymous && !document.AllowUsernamePassword)
        {
            issues.Add(new LocalServerConfigurationValidationIssue(
                nameof(document.AllowAnonymous),
                "At least one user token policy must be enabled."));
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

    private static bool ContainsOrdinalIgnoreCase(IEnumerable<string> values, string? value)
    {
        foreach (var candidate in values)
        {
            if (string.Equals(candidate, value, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}