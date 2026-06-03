namespace UAGateway.Core.Diagnostics;

public static class UAGatewayLogPaths
{
    public static string LogsDirectoryPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "UA Gateway", "logs");
}
