using Microsoft.UI.Xaml.Controls;
using UAGateway.Core.Diagnostics;
using UAGateway.Core.Health;
using UAGateway.Core.Ipc;

namespace UAGateway.UI.Controls;

public sealed partial class DashboardOverview : UserControl
{
    public DashboardOverview()
    {
        InitializeComponent();
    }

    public void RefreshDiagnostics()
    {
        LoadSecurityDiagnostics();
        LoadConnectionDiagnostics();
    }

    public void ShowStartupHealth(IpcStartupHealthSnapshotPayload? payload)
    {
        if (payload is null || !payload.Available || payload.Snapshot is null)
        {
            StartupStatusText.Text = "Status: Unknown";
            StartupReasonText.Text = "Reason: IPC startup health unavailable.";
            StartupUpdatedText.Text = "Updated (UTC): n/a";
            return;
        }

        var snapshot = payload.Snapshot;
        StartupStatusText.Text = $"Status: {snapshot.Status}";
        StartupReasonText.Text = $"Reason: {snapshot.Reason}";
        StartupUpdatedText.Text = $"Updated (UTC): {snapshot.UpdatedUtc:O}";
    }

    public void ShowConnectionSnapshot(IpcConnectionSnapshotPayload? payload)
    {
        if (payload is null || !payload.Available || payload.Snapshot is null)
        {
            return;
        }

        var snapshot = payload.Snapshot;
        ConnectionSummaryText.Text = $"Enabled endpoints: {snapshot.EnabledEndpointCount}";
        ConnectionStateText.Text =
            $"Connected={snapshot.ConnectedEndpointCount} Connecting={snapshot.ConnectingEndpointCount} Disconnected={snapshot.DisconnectedEndpointCount} Failures={snapshot.TotalFailureCount}";
        ConnectionUpdatedText.Text = $"Updated (UTC): {snapshot.UpdatedUtc:O} via IPC";
    }

    public void ShowSecuritySnapshot(IpcSecurityBootstrapSnapshotPayload? payload)
    {
        if (payload is null || !payload.Available || payload.Snapshot is null)
        {
            return;
        }

        var snapshot = payload.Snapshot;
        SecurityStatusText.Text = $"Status: {snapshot.Status}";
        SecurityReasonText.Text = $"Reason: {snapshot.Reason}";
        SecurityCountsText.Text = $"Trust Counts: peers={snapshot.TrustedPeerCount}, issuers={snapshot.TrustedIssuerCount}, rejected={snapshot.RejectedCount}";
        SecurityThumbprintText.Text = $"Thumbprint: {snapshot.CertificateThumbprint ?? "n/a"}";
        SecurityUpdatedText.Text = $"Updated (UTC): {snapshot.UpdatedUtc:O} via IPC";
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
}