using System.Text.Json;
using UAGateway.Core.Configuration;
using Xunit;

namespace UAGateway.Core.Tests;

public sealed class UpstreamEndpointConfigurationStoreTests
{
    [Fact]
    public void LoadOrCreateDefault_WhenMissingFile_CreatesEmptyDocument()
    {
        var filePath = GetTempFilePath("upstream-endpoints.json");
        try
        {
            var dir = Path.GetDirectoryName(filePath)!;
            var document = UpstreamEndpointConfigurationStore.LoadOrCreateDefault(dir);
            Assert.NotNull(document);
            Assert.Empty(document.Endpoints);
            Assert.True(File.Exists(filePath));
        }
        finally { TryDelete(filePath); }
    }

    [Fact]
    public void LoadOrCreateDefault_WhenMalformedJson_ThrowsInvalidOperationWithJsonCause()
    {
        var filePath = GetTempFilePath("upstream-endpoints.json");
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            File.WriteAllText(filePath, "{ not valid json }");

            var ex = Assert.Throws<InvalidOperationException>(() =>
                UpstreamEndpointConfigurationStore.LoadOrCreateDefault(Path.GetDirectoryName(filePath)!));

            Assert.Contains("invalid JSON", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.IsType<JsonException>(ex.InnerException);
        }
        finally { TryDelete(filePath); }
    }

    [Fact]
    public void LoadOrCreateDefault_WhenNullPayload_ThrowsInvalidOperation()
    {
        var filePath = GetTempFilePath("upstream-endpoints.json");
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            File.WriteAllText(filePath, "null");

            var ex = Assert.Throws<InvalidOperationException>(() =>
                UpstreamEndpointConfigurationStore.LoadOrCreateDefault(Path.GetDirectoryName(filePath)!));

            Assert.Contains("empty or invalid", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally { TryDelete(filePath); }
    }

    private static string GetTempFilePath(string fileName)
    {
        var dir = Path.Combine(Path.GetTempPath(), "ua-gateway-tests", Guid.NewGuid().ToString("N"));
        return Path.Combine(dir, fileName);
    }

    private static void TryDelete(string filePath)
    {
        try { Directory.Delete(Path.GetDirectoryName(filePath)!, recursive: true); }
        catch { }
    }
}

public sealed class NamespaceMappingConfigurationStoreTests
{
    [Fact]
    public void LoadOrCreateDefault_WhenMissingFile_CreatesEmptyDocument()
    {
        var filePath = GetTempFilePath("namespace-mapping.json");
        try
        {
            var dir = Path.GetDirectoryName(filePath)!;
            var document = NamespaceMappingConfigurationStore.LoadOrCreateDefault(dir);
            Assert.NotNull(document);
            Assert.Empty(document.Rules);
            Assert.True(File.Exists(filePath));
        }
        finally { TryDelete(filePath); }
    }

    [Fact]
    public void LoadOrCreateDefault_WhenMalformedJson_ThrowsInvalidOperationWithJsonCause()
    {
        var filePath = GetTempFilePath("namespace-mapping.json");
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            File.WriteAllText(filePath, "[ broken");

            var ex = Assert.Throws<InvalidOperationException>(() =>
                NamespaceMappingConfigurationStore.LoadOrCreateDefault(Path.GetDirectoryName(filePath)!));

            Assert.Contains("invalid JSON", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.IsType<JsonException>(ex.InnerException);
        }
        finally { TryDelete(filePath); }
    }

    [Fact]
    public void LoadOrCreateDefault_WhenNullPayload_ThrowsInvalidOperation()
    {
        var filePath = GetTempFilePath("namespace-mapping.json");
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            File.WriteAllText(filePath, "null");

            var ex = Assert.Throws<InvalidOperationException>(() =>
                NamespaceMappingConfigurationStore.LoadOrCreateDefault(Path.GetDirectoryName(filePath)!));

            Assert.Contains("empty or invalid", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally { TryDelete(filePath); }
    }

    private static string GetTempFilePath(string fileName)
    {
        var dir = Path.Combine(Path.GetTempPath(), "ua-gateway-tests", Guid.NewGuid().ToString("N"));
        return Path.Combine(dir, fileName);
    }

    private static void TryDelete(string filePath)
    {
        try { Directory.Delete(Path.GetDirectoryName(filePath)!, recursive: true); }
        catch { }
    }
}