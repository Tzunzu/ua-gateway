using System.Text.Json;

namespace UAGateway.Core.Diagnostics;

public sealed record ConnectionLifecycleDiagnosticsSnapshot(
    DateTimeOffset UpdatedUtc,
    int EnabledEndpointCount,
    int ConnectedEndpointCount,
    int ConnectingEndpointCount,
    int DisconnectedEndpointCount,
    int TotalFailureCount);

public static class ConnectionLifecycleDiagnosticsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    public static string DiagnosticsDirectoryPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "UA Gateway", "diagnostics");

    public static string DiagnosticsFilePath =>
        Path.Combine(DiagnosticsDirectoryPath, "connection-lifecycle.json");

    public static void Save(ConnectionLifecycleDiagnosticsSnapshot snapshot)
    {
        Directory.CreateDirectory(DiagnosticsDirectoryPath);
        var json = JsonSerializer.Serialize(snapshot, JsonOptions);
        File.WriteAllText(DiagnosticsFilePath, json);
    }

    public static bool TryLoad(out ConnectionLifecycleDiagnosticsSnapshot? snapshot)
    {
        snapshot = null;

        if (!File.Exists(DiagnosticsFilePath))
        {
            return false;
        }

        try
        {
            var json = File.ReadAllText(DiagnosticsFilePath);
            snapshot = JsonSerializer.Deserialize<ConnectionLifecycleDiagnosticsSnapshot>(json, JsonOptions);
            return snapshot is not null;
        }
        catch
        {
            snapshot = null;
            return false;
        }
    }
}
