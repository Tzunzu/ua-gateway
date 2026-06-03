using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using UAGateway.Core.Diagnostics;

namespace UAGateway.UI.Controls;

public sealed partial class LogsViewer : UserControl
{
    private List<string> _allLogLines = [];

    public LogsViewer()
    {
        InitializeComponent();
    }

    public void ReloadLogs()
    {
        Directory.CreateDirectory(UAGatewayLogPaths.LogsDirectoryPath);

        var latestLogFile = Directory
            .EnumerateFiles(UAGatewayLogPaths.LogsDirectoryPath, "ua-gateway-*.log", SearchOption.TopDirectoryOnly)
            .OrderByDescending(path => File.GetLastWriteTimeUtc(path))
            .FirstOrDefault();

        if (latestLogFile is null)
        {
            _allLogLines = [];
            LogsSummaryText.Text = $"No logs found at {UAGatewayLogPaths.LogsDirectoryPath}";
            LogsList.Items.Clear();
            return;
        }

        _allLogLines = File.ReadAllLines(latestLogFile)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();

        LogsSummaryText.Text = $"Loaded {_allLogLines.Count} lines from {latestLogFile}";
        ApplyLogFilters();
    }

    private void ApplyLogFilters_Click(object sender, RoutedEventArgs e)
    {
        ApplyLogFilters();
    }

    private void ApplyLogFilters()
    {
        IEnumerable<string> filtered = _allLogLines;

        var severity = SeverityFilterComboBox.SelectedItem as string ?? "All Severities";
        filtered = severity switch
        {
            "Information" => filtered.Where(line => line.Contains("[INF]", StringComparison.OrdinalIgnoreCase)),
            "Warning" => filtered.Where(line => line.Contains("[WRN]", StringComparison.OrdinalIgnoreCase)),
            "Error" => filtered.Where(line => line.Contains("[ERR]", StringComparison.OrdinalIgnoreCase)),
            "Critical" => filtered.Where(line => line.Contains("[FTL]", StringComparison.OrdinalIgnoreCase)),
            _ => filtered,
        };

        var categoryText = (CategoryFilterTextBox.Text ?? string.Empty).Trim();
        if (categoryText.Length > 0)
        {
            filtered = filtered.Where(line => line.Contains(categoryText, StringComparison.OrdinalIgnoreCase));
        }

        var eventIdText = (EventIdFilterTextBox.Text ?? string.Empty).Trim();
        if (eventIdText.Length > 0)
        {
            filtered = filtered.Where(line => line.Contains(eventIdText, StringComparison.OrdinalIgnoreCase));
        }

        var lines = filtered.TakeLast(500).ToList();

        LogsList.Items.Clear();
        foreach (var line in lines)
        {
            LogsList.Items.Add(line);
        }

        LogsSummaryText.Text = $"Showing {lines.Count} filtered lines from {UAGatewayLogPaths.LogsDirectoryPath}";
    }
}