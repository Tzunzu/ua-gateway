using Microsoft.UI.Xaml;
using UAGateway.Core.Diagnostics;

namespace UAGateway.UI;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        LoadSecurityDiagnostics();
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
}
