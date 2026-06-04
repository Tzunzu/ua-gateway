using System.Text.Json;

namespace UAGateway.Core.Configuration;

public static class LocalServerConfigurationStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    public static string ConfigurationDirectoryPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "UA Gateway", "config");

    public static string ConfigurationFilePath =>
        Path.Combine(ConfigurationDirectoryPath, "server-settings.json");

    public static LocalServerConfigurationDocument LoadOrCreateDefault(string? configurationDirectoryPathOverride = null)
    {
        var filePath = GetConfigurationFilePath(configurationDirectoryPathOverride);

        if (!File.Exists(filePath))
        {
            var defaultDocument = new LocalServerConfigurationDocument();
            Save(defaultDocument, configurationDirectoryPathOverride);
            return defaultDocument;
        }

        var json = File.ReadAllText(filePath);

        LocalServerConfigurationDocument? document;
        try
        {
            document = JsonSerializer.Deserialize<LocalServerConfigurationDocument>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                "Local server configuration file contains invalid JSON.",
                ex);
        }

        if (document is null)
        {
            throw new InvalidOperationException("Local server configuration file is empty or invalid.");
        }

        return document;
    }

    public static void Save(LocalServerConfigurationDocument document, string? configurationDirectoryPathOverride = null)
    {
        var directoryPath = configurationDirectoryPathOverride ?? ConfigurationDirectoryPath;
        var filePath = GetConfigurationFilePath(configurationDirectoryPathOverride);

        Directory.CreateDirectory(directoryPath);
        var json = JsonSerializer.Serialize(document, JsonOptions);
        File.WriteAllText(filePath, json);
    }

    private static string GetConfigurationFilePath(string? configurationDirectoryPathOverride)
    {
        var directoryPath = configurationDirectoryPathOverride ?? ConfigurationDirectoryPath;
        return Path.Combine(directoryPath, "server-settings.json");
    }
}