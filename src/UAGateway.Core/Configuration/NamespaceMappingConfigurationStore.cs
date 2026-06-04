using System.Text.Json;

namespace UAGateway.Core.Configuration;

public static class NamespaceMappingConfigurationStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    public static string ConfigurationDirectoryPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "UA Gateway", "config");

    public static string ConfigurationFilePath =>
        Path.Combine(ConfigurationDirectoryPath, "namespace-mapping.json");

    public static NamespaceMappingConfigurationDocument LoadOrCreateDefault()
    {
        if (!File.Exists(ConfigurationFilePath))
        {
            var defaultDocument = new NamespaceMappingConfigurationDocument();
            Save(defaultDocument);
            return defaultDocument;
        }

        var json = File.ReadAllText(ConfigurationFilePath);

        NamespaceMappingConfigurationDocument? document;
        try
        {
            document = JsonSerializer.Deserialize<NamespaceMappingConfigurationDocument>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                "Namespace mapping configuration file contains invalid JSON.",
                ex);
        }

        if (document is null)
        {
            throw new InvalidOperationException("Namespace mapping configuration file is empty or invalid.");
        }

        return document;
    }

    public static void Save(NamespaceMappingConfigurationDocument document)
    {
        Directory.CreateDirectory(ConfigurationDirectoryPath);
        var json = JsonSerializer.Serialize(document, JsonOptions);
        File.WriteAllText(ConfigurationFilePath, json);
    }

    public static NamespaceMappingConfigurationDocument LoadOrCreateDefault(string configurationDirectoryPathOverride)
    {
        var filePath = Path.Combine(configurationDirectoryPathOverride, "namespace-mapping.json");

        if (!File.Exists(filePath))
        {
            var emptyDocument = new NamespaceMappingConfigurationDocument();
            Save(emptyDocument, configurationDirectoryPathOverride);
            return emptyDocument;
        }

        var json = File.ReadAllText(filePath);

        NamespaceMappingConfigurationDocument? document;
        try
        {
            document = JsonSerializer.Deserialize<NamespaceMappingConfigurationDocument>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                "Namespace mapping configuration file contains invalid JSON.",
                ex);
        }

        if (document is null)
        {
            throw new InvalidOperationException("Namespace mapping configuration file is empty or invalid.");
        }

        return document;
    }

    public static void Save(NamespaceMappingConfigurationDocument document, string configurationDirectoryPathOverride)
    {
        Directory.CreateDirectory(configurationDirectoryPathOverride);
        var filePath = Path.Combine(configurationDirectoryPathOverride, "namespace-mapping.json");
        var json = JsonSerializer.Serialize(document, JsonOptions);
        File.WriteAllText(filePath, json);
    }
}
