using System.Text.Json;

namespace UAGateway.Core.Configuration;

public static class UpstreamEndpointConfigurationStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    public static string ConfigurationDirectoryPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "UA Gateway", "config");

    public static string ConfigurationFilePath =>
        Path.Combine(ConfigurationDirectoryPath, "upstream-endpoints.json");

    public static UpstreamEndpointConfigurationDocument LoadOrCreateDefault()
    {
        if (!File.Exists(ConfigurationFilePath))
        {
            var emptyDocument = new UpstreamEndpointConfigurationDocument();
            Save(emptyDocument);
            return emptyDocument;
        }

        var json = File.ReadAllText(ConfigurationFilePath);
        var document = JsonSerializer.Deserialize<UpstreamEndpointConfigurationDocument>(json, JsonOptions);

        if (document is null)
        {
            throw new InvalidOperationException("Upstream endpoint configuration file is empty or invalid.");
        }

        return document;
    }

    public static void Save(UpstreamEndpointConfigurationDocument document)
    {
        Directory.CreateDirectory(ConfigurationDirectoryPath);
        var json = JsonSerializer.Serialize(document, JsonOptions);
        File.WriteAllText(ConfigurationFilePath, json);
    }
}
