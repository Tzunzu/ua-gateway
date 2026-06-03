using System.Text.Json;

namespace UAGateway.Core.Diagnostics;

public enum SecurityBootstrapStatus
{
    Unknown,
    Healthy,
    Degraded,
    Faulted,
}

public sealed record SecurityBootstrapDiagnosticsSnapshot(
    SecurityBootstrapStatus Status,
    string Reason,
    DateTimeOffset UpdatedUtc,
    string? CorrelationId,
    string? CertificateThumbprint,
    int TrustedPeerCount,
    int TrustedIssuerCount,
    int RejectedCount,
    bool AutoAcceptUntrustedCertificates,
    bool RejectSha1SignedCertificates,
    ushort MinimumCertificateKeySize);

public static class SecurityBootstrapDiagnosticsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    public static string DiagnosticsDirectoryPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "UA Gateway", "diagnostics");

    public static string DiagnosticsFilePath =>
        Path.Combine(DiagnosticsDirectoryPath, "security-bootstrap.json");

    public static void Save(SecurityBootstrapDiagnosticsSnapshot snapshot)
    {
        Directory.CreateDirectory(DiagnosticsDirectoryPath);

        var json = JsonSerializer.Serialize(snapshot, JsonOptions);
        File.WriteAllText(DiagnosticsFilePath, json);
    }

    public static bool TryLoad(out SecurityBootstrapDiagnosticsSnapshot? snapshot)
    {
        snapshot = null;

        if (!File.Exists(DiagnosticsFilePath))
        {
            return false;
        }

        try
        {
            var json = File.ReadAllText(DiagnosticsFilePath);
            snapshot = JsonSerializer.Deserialize<SecurityBootstrapDiagnosticsSnapshot>(json, JsonOptions);
            return snapshot is not null;
        }
        catch
        {
            snapshot = null;
            return false;
        }
    }
}
