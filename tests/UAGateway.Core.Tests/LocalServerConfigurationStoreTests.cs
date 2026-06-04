using System.Text.Json;
using UAGateway.Core.Configuration;
using Xunit;

namespace UAGateway.Core.Tests;

public sealed class LocalServerConfigurationStoreTests
{
    [Fact]
    public void LoadOrCreateDefault_WhenMissing_CreatesDefaultDocument()
    {
        var directory = CreateTempDirectory();

        try
        {
            var document = LocalServerConfigurationStore.LoadOrCreateDefault(directory);

            Assert.Equal("localhost", document.Host);
            Assert.Equal(4840, document.Port);
            Assert.Equal("UAGateway", document.EndpointPath);
            Assert.True(File.Exists(Path.Combine(directory, "server-settings.json")));
        }
        finally
        {
            TryDeleteDirectory(directory);
        }
    }

    [Fact]
    public void LoadOrCreateDefault_WhenMalformedJson_ThrowsInvalidOperation()
    {
        var directory = CreateTempDirectory();
        var filePath = Path.Combine(directory, "server-settings.json");

        try
        {
            File.WriteAllText(filePath, "{ malformed-json }");

            var ex = Assert.Throws<InvalidOperationException>(() =>
                LocalServerConfigurationStore.LoadOrCreateDefault(directory));

            Assert.Contains("invalid JSON", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.NotNull(ex.InnerException);
            Assert.IsType<JsonException>(ex.InnerException);
        }
        finally
        {
            TryDeleteDirectory(directory);
        }
    }

    [Fact]
    public void LoadOrCreateDefault_WhenNullPayload_ThrowsInvalidOperation()
    {
        var directory = CreateTempDirectory();
        var filePath = Path.Combine(directory, "server-settings.json");

        try
        {
            File.WriteAllText(filePath, "null");

            var ex = Assert.Throws<InvalidOperationException>(() =>
                LocalServerConfigurationStore.LoadOrCreateDefault(directory));

            Assert.Contains("empty or invalid", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            TryDeleteDirectory(directory);
        }
    }

    [Fact]
    public void Validator_WhenLoadedSettingsOutOfRange_FlagsPortIssue()
    {
        var directory = CreateTempDirectory();

        try
        {
            LocalServerConfigurationStore.Save(new LocalServerConfigurationDocument
            {
                Host = "localhost",
                Port = 70000,
                EndpointPath = "UAGateway",
            }, directory);

            var document = LocalServerConfigurationStore.LoadOrCreateDefault(directory);
            var issues = LocalServerConfigurationValidator.Validate(document);

            Assert.Contains(issues, issue =>
                issue.Setting == nameof(LocalServerConfigurationDocument.Port) &&
                issue.Message.Contains("between 1 and 65535", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            TryDeleteDirectory(directory);
        }
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "ua-gateway-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // Best-effort cleanup for test temp files.
        }
    }
}
