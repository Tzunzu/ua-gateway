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
        var document = JsonSerializer.Deserialize<NamespaceMappingConfigurationDocument>(json, JsonOptions);

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
}
