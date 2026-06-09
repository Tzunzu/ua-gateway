using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace UAGateway.Core.Configuration;

public sealed record EndpointUsernamePasswordCredential(string Username, string Password);

public static class UpstreamEndpointCredentialStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    public static string ConfigurationDirectoryPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "UA Gateway", "config");

    public static string CredentialsFilePath =>
        Path.Combine(ConfigurationDirectoryPath, "upstream-endpoint-credentials.json");

    public static void SaveUsernamePassword(string credentialId, string username, string password, string? configurationDirectoryPathOverride = null)
    {
        if (string.IsNullOrWhiteSpace(credentialId))
        {
            throw new ArgumentException("Credential id is required.", nameof(credentialId));
        }

        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Credential encryption requires Windows DPAPI.");
        }

        var store = LoadOrCreate(configurationDirectoryPathOverride);
        var payload = JsonSerializer.Serialize(new EndpointUsernamePasswordCredential(username, password), JsonOptions);
        var protectedBytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(payload), optionalEntropy: null, DataProtectionScope.CurrentUser);
        store.Entries[credentialId] = Convert.ToBase64String(protectedBytes);
        SaveStore(store, configurationDirectoryPathOverride);
    }

    public static EndpointUsernamePasswordCredential? TryLoadUsernamePassword(string credentialId, string? configurationDirectoryPathOverride = null)
    {
        if (string.IsNullOrWhiteSpace(credentialId))
        {
            return null;
        }

        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        var store = LoadOrCreate(configurationDirectoryPathOverride);
        if (!store.Entries.TryGetValue(credentialId, out var encryptedPayload) || string.IsNullOrWhiteSpace(encryptedPayload))
        {
            return null;
        }

        try
        {
            var protectedBytes = Convert.FromBase64String(encryptedPayload);
            var plainBytes = ProtectedData.Unprotect(protectedBytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
            var payload = Encoding.UTF8.GetString(plainBytes);
            return JsonSerializer.Deserialize<EndpointUsernamePasswordCredential>(payload, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    public static void RemoveCredential(string credentialId, string? configurationDirectoryPathOverride = null)
    {
        if (string.IsNullOrWhiteSpace(credentialId))
        {
            return;
        }

        var store = LoadOrCreate(configurationDirectoryPathOverride);
        if (!store.Entries.Remove(credentialId))
        {
            return;
        }

        SaveStore(store, configurationDirectoryPathOverride);
    }

    private static UpstreamEndpointCredentialDocument LoadOrCreate(string? configurationDirectoryPathOverride)
    {
        var filePath = GetCredentialsFilePath(configurationDirectoryPathOverride);

        if (!File.Exists(filePath))
        {
            var empty = new UpstreamEndpointCredentialDocument();
            SaveStore(empty, configurationDirectoryPathOverride);
            return empty;
        }

        var json = File.ReadAllText(filePath);
        try
        {
            var document = JsonSerializer.Deserialize<UpstreamEndpointCredentialDocument>(json, JsonOptions);
            return document ?? new UpstreamEndpointCredentialDocument();
        }
        catch
        {
            return new UpstreamEndpointCredentialDocument();
        }
    }

    private static void SaveStore(UpstreamEndpointCredentialDocument document, string? configurationDirectoryPathOverride)
    {
        var directoryPath = configurationDirectoryPathOverride ?? ConfigurationDirectoryPath;
        var filePath = GetCredentialsFilePath(configurationDirectoryPathOverride);
        Directory.CreateDirectory(directoryPath);
        var json = JsonSerializer.Serialize(document, JsonOptions);
        File.WriteAllText(filePath, json);
    }

    private static string GetCredentialsFilePath(string? configurationDirectoryPathOverride)
    {
        var directoryPath = configurationDirectoryPathOverride ?? ConfigurationDirectoryPath;
        return Path.Combine(directoryPath, "upstream-endpoint-credentials.json");
    }

    private sealed record UpstreamEndpointCredentialDocument
    {
        public Dictionary<string, string> Entries { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
