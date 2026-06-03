using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Windowing;
using UAGateway.Core.Configuration;
using UAGateway.Core.Diagnostics;
using Windows.Graphics;
using WinRT.Interop;

namespace UAGateway.UI;

public sealed partial class MainWindow : Window
{
    private List<string> _allLogLines = [];
    private UpstreamEndpointConfigurationDocument _draftEndpoints = new();

    public MainWindow()
    {
        InitializeComponent();

        EnsureVisibleOnLaunch();
        LoadInitialState();
    }

    private void LoadInitialState()
    {
        try
        {
            LoadSecurityDiagnostics();
            LoadConnectionDiagnostics();
            ReloadConnectionsDraft();
            LoadLogs();
        }
        catch (Exception ex)
        {
            ConnectionsApplyStatusText.Text = $"Startup warning: {ex.Message}";
        }
    }

    private void EnsureVisibleOnLaunch()
    {
        try
        {
            var hWnd = WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
            var appWindow = AppWindow.GetFromWindowId(windowId);

            appWindow.Resize(new SizeInt32(1280, 840));
            appWindow.Move(new PointInt32(80, 60));
        }
        catch
        {
            // Best-effort positioning only.
        }
    }

    private void LoadSecurityDiagnostics()
    {
        if (!SecurityBootstrapDiagnosticsStore.TryLoad(out var snapshot) || snapshot is null)
        {
            SecurityStatusText.Text = "Status: Unknown";
            SecurityReasonText.Text = "Reason: No diagnostics snapshot found. Start the service to populate diagnostics.";
            SecurityCountsText.Text = "Trust Counts: peers=0, issuers=0, rejected=0";
            SecurityThumbprintText.Text = "Thumbprint: n/a";
            SecurityUpdatedText.Text = $"Diagnostics file: {SecurityBootstrapDiagnosticsStore.DiagnosticsFilePath}";
            return;
        }

        SecurityStatusText.Text = $"Status: {snapshot.Status}";
        SecurityReasonText.Text = $"Reason: {snapshot.Reason}";
        SecurityCountsText.Text = $"Trust Counts: peers={snapshot.TrustedPeerCount}, issuers={snapshot.TrustedIssuerCount}, rejected={snapshot.RejectedCount}";
        SecurityThumbprintText.Text = $"Thumbprint: {snapshot.CertificateThumbprint ?? "n/a"}";
        SecurityUpdatedText.Text = $"Updated (UTC): {snapshot.UpdatedUtc:O}";
    }

    private void LoadConnectionDiagnostics()
    {
        if (!ConnectionLifecycleDiagnosticsStore.TryLoad(out var snapshot) || snapshot is null)
        {
            ConnectionSummaryText.Text = "No connection metrics snapshot found yet.";
            ConnectionStateText.Text = "Enabled=0 Connected=0 Connecting=0 Disconnected=0 Failures=0";
            ConnectionUpdatedText.Text = $"Diagnostics file: {ConnectionLifecycleDiagnosticsStore.DiagnosticsFilePath}";
            return;
        }

        ConnectionSummaryText.Text = $"Enabled endpoints: {snapshot.EnabledEndpointCount}";
        ConnectionStateText.Text =
            $"Connected={snapshot.ConnectedEndpointCount} Connecting={snapshot.ConnectingEndpointCount} Disconnected={snapshot.DisconnectedEndpointCount} Failures={snapshot.TotalFailureCount}";
        ConnectionUpdatedText.Text = $"Updated (UTC): {snapshot.UpdatedUtc:O}";
    }

    private void ReloadConnectionsDraft()
    {
        _draftEndpoints = UpstreamEndpointConfigurationStore.LoadOrCreateDefault();
        ConnectionsApplyStatusText.Text = "Draft reloaded from disk.";
        RenderConnectionsDraft();
    }

    private void RenderConnectionsDraft()
    {
        ConnectionsHeaderText.Text = $"Draft endpoints: {_draftEndpoints.Endpoints.Count}";

        ConnectionsList.Items.Clear();

        foreach (var endpoint in _draftEndpoints.Endpoints)
        {
            var enabledFlag = endpoint.Enabled ? "Enabled" : "Disabled";
            ConnectionsList.Items.Add($"{endpoint.DisplayName} | {enabledFlag} | {endpoint.EndpointUrl}");
        }
    }

    private void AddConnectionDraft_Click(object sender, RoutedEventArgs e)
    {
        var displayName = (DraftDisplayNameTextBox.Text ?? string.Empty).Trim();
        var endpointUrl = (DraftEndpointUrlTextBox.Text ?? string.Empty).Trim();

        var newEndpoint = new UpstreamEndpointConfiguration
        {
            Id = Guid.NewGuid().ToString("N"),
            DisplayName = displayName,
            EndpointUrl = endpointUrl,
            Enabled = DraftEnabledToggle.IsOn,
        };

        _draftEndpoints.Endpoints.Add(newEndpoint);
        RenderConnectionsDraft();

        DraftDisplayNameTextBox.Text = string.Empty;
        DraftEndpointUrlTextBox.Text = string.Empty;
        DraftEnabledToggle.IsOn = true;
        ConnectionsApplyStatusText.Text = "Endpoint added to draft. Apply Draft to persist.";
    }

    private void ReloadConnectionsDraft_Click(object sender, RoutedEventArgs e)
    {
        ReloadConnectionsDraft();
    }

    private void ApplyConnectionsDraft_Click(object sender, RoutedEventArgs e)
    {
        var issues = UpstreamEndpointConfigurationValidator.Validate(_draftEndpoints);
        if (issues.Count > 0)
        {
            var firstIssue = issues[0];
            ConnectionsApplyStatusText.Text =
                $"Apply failed. Validation issues: {issues.Count}. First issue: [{firstIssue.EndpointId}] {firstIssue.Message}";
            return;
        }

        UpstreamEndpointConfigurationStore.Save(_draftEndpoints);
        ConnectionsApplyStatusText.Text = "Apply succeeded. Draft saved to upstream endpoint configuration file.";
        RenderConnectionsDraft();
    }

    private void LoadLogs()
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
