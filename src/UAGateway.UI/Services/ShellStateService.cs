using UAGateway.Core.Health;
using UAGateway.Core.Diagnostics;
using UAGateway.Core.Ipc;

namespace UAGateway.UI.Services;

public sealed class ShellStateService
{
    private readonly IpcControlClient _ipcControlClient = new();

    public IpcStartupHealthSnapshotPayload? LatestStartupHealth { get; private set; }

    public IpcConnectionSnapshotPayload? LatestConnectionSnapshot { get; private set; }

    public IpcSecurityBootstrapSnapshotPayload? LatestSecuritySnapshot { get; private set; }

    public bool IpcConnected { get; private set; }

    public bool IsEventStreamConnectionKnown { get; private set; }

    public bool IsEventStreamConnected { get; private set; }

    public void Reset()
    {
        LatestStartupHealth = null;
        LatestConnectionSnapshot = null;
        LatestSecuritySnapshot = null;
        IpcConnected = false;
    }

    public void SetEventStreamState(bool isKnown, bool isConnected)
    {
        IsEventStreamConnectionKnown = isKnown;
        IsEventStreamConnected = isConnected;
    }

    public async Task<IpcHandshakeResponse?> CheckIpcHandshakeAsync(CancellationToken cancellationToken = default)
    {
        var handshake = await _ipcControlClient.TryHandshakeAsync(cancellationToken);
        if (handshake is null)
        {
            IpcConnected = false;
            return null;
        }

        LatestStartupHealth = await _ipcControlClient.TryGetStartupHealthAsync(cancellationToken);
        LatestConnectionSnapshot = await _ipcControlClient.TryGetConnectionSnapshotAsync(cancellationToken);
        LatestSecuritySnapshot = await _ipcControlClient.TryGetSecurityBootstrapAsync(cancellationToken);

        IpcConnected = true;
        return handshake;
    }

    public async Task RefreshSnapshotsAsync(CancellationToken cancellationToken = default)
    {
        if (!IpcConnected)
        {
            await CheckIpcHandshakeAsync(cancellationToken);
            return;
        }

        LatestStartupHealth = await _ipcControlClient.TryGetStartupHealthAsync(cancellationToken);
        LatestConnectionSnapshot = await _ipcControlClient.TryGetConnectionSnapshotAsync(cancellationToken);
        LatestSecuritySnapshot = await _ipcControlClient.TryGetSecurityBootstrapAsync(cancellationToken);
    }

    public ServiceStatusEvaluation EvaluateServiceStatus()
    {
        if (!IpcConnected)
        {
            return new ServiceStatusEvaluation(
                "Service Status: Offline (IPC control channel unavailable)",
                ShellStatusAction.RetryIpc,
                ShellStatusAction.OpenLogs);
        }

        if (LatestStartupHealth?.Snapshot?.Status == StartupHealthStatus.Faulted)
        {
            var reason = BuildStartupReasonSuffix(LatestStartupHealth.Snapshot.Reason, fallback: "Service startup failed");
            return new ServiceStatusEvaluation(
                $"Service Status: Failed ({reason})",
                ShellStatusAction.OpenLogs,
                ShellStatusAction.RetryIpc);
        }

        if (LatestStartupHealth?.Snapshot?.Status == StartupHealthStatus.Degraded)
        {
            var reason = BuildStartupReasonSuffix(LatestStartupHealth.Snapshot.Reason, fallback: "Startup validation warnings");
            return new ServiceStatusEvaluation(
                $"Service Status: Limited ({reason})",
                ShellStatusAction.OpenLogs,
                ShellStatusAction.Refresh);
        }

        if (LatestSecuritySnapshot is null || !LatestSecuritySnapshot.Available || LatestSecuritySnapshot.Snapshot is null)
        {
            return new ServiceStatusEvaluation(
                "Service Status: Limited (No security snapshot)",
                ShellStatusAction.OpenLogs,
                ShellStatusAction.Refresh);
        }

        if (LatestSecuritySnapshot.Snapshot.Status == SecurityBootstrapStatus.Faulted)
        {
            return new ServiceStatusEvaluation(
                "Service Status: Failed (Security bootstrap faulted)",
                ShellStatusAction.OpenLogs,
                ShellStatusAction.RetryIpc);
        }

        if (LatestSecuritySnapshot.Snapshot.Status == SecurityBootstrapStatus.Degraded)
        {
            return new ServiceStatusEvaluation(
                "Service Status: Limited (Security trust configuration needs attention)",
                ShellStatusAction.OpenLogs,
                ShellStatusAction.Refresh);
        }

        if (LatestConnectionSnapshot?.Snapshot is null)
        {
            return new ServiceStatusEvaluation(
                "Service Status: Limited (Snapshot refresh pending)",
                ShellStatusAction.Refresh,
                ShellStatusAction.OpenLogs);
        }

        var connected = LatestConnectionSnapshot.Snapshot.ConnectedEndpointCount;
        var enabled = LatestConnectionSnapshot.Snapshot.EnabledEndpointCount;

        if (connected < enabled)
        {
            return new ServiceStatusEvaluation(
                $"Service Status: Limited (Partial upstream connectivity {connected}/{enabled} connected)",
                ShellStatusAction.OpenConnections,
                ShellStatusAction.Refresh);
        }

        if (IsEventStreamConnectionKnown && !IsEventStreamConnected)
        {
            return new ServiceStatusEvaluation(
                "Service Status: Limited (Live event stream disconnected)",
                ShellStatusAction.RetryIpc,
                ShellStatusAction.Refresh);
        }

        return new ServiceStatusEvaluation(
            "Service Status: Connected (All monitored services nominal)",
            ShellStatusAction.None,
            ShellStatusAction.None);
    }

    private static string BuildStartupReasonSuffix(string? startupReason, string fallback)
    {
        if (string.IsNullOrWhiteSpace(startupReason))
        {
            return fallback;
        }

        var normalized = startupReason.Trim();
        if (normalized.StartsWith("Startup failed:", StringComparison.OrdinalIgnoreCase))
        {
            return normalized["Startup failed:".Length..].Trim();
        }

        return normalized;
    }
}

public readonly record struct ServiceStatusEvaluation(
    string Text,
    ShellStatusAction PrimaryAction,
    ShellStatusAction SecondaryAction);

public enum ShellStatusAction
{
    None,
    RetryIpc,
    Refresh,
    OpenLogs,
    OpenConnections,
}
