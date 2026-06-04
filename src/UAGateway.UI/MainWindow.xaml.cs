using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Windowing;
using UAGateway.Core.Configuration;
using UAGateway.Core.Diagnostics;
using UAGateway.Core.Ipc;
using UAGateway.UI.Services;
using UAGateway.Core.Health;
using Windows.UI;
using Windows.Graphics;
using WinRT.Interop;

namespace UAGateway.UI;

public sealed partial class MainWindow : Window
{
    private readonly IpcControlClient _ipcControlClient = new();
    private IpcStartupHealthSnapshotPayload? _latestStartupHealth;
    private IpcConnectionSnapshotPayload? _latestConnectionSnapshot;
    private IpcSecurityBootstrapSnapshotPayload? _latestSecuritySnapshot;
    private bool _ipcConnected;
    private StatusAction _primaryStatusAction;
    private StatusAction _secondaryStatusAction;
    private HelpWindow? _helpWindow;
    private ElementTheme _selectedTheme = ElementTheme.Dark;
    private ShellPalette _selectedPalette = ShellPalette.WinUI;

    public MainWindow()
    {
        InitializeComponent();

        RootLayout.ActualThemeChanged += RootLayout_ActualThemeChanged;
        LiveOutputViewerView.StreamConnectionStateChanged += LiveOutputViewerView_StreamConnectionStateChanged;
        ApplyTheme(_selectedTheme);
        ApplyShellPalette();

        EnsureVisibleOnLaunch();
        LoadInitialState();
        _ = CheckIpcHandshakeAsync();
    }

    private void LoadInitialState()
    {
        _latestStartupHealth = null;
        _latestConnectionSnapshot = null;
        _latestSecuritySnapshot = null;
        _ipcConnected = false;
        UpdateStatusBar();

        try
        {
            DashboardOverviewView.RefreshDiagnostics();
            DashboardOverviewView.ShowStartupHealth(null);
            ConnectionsEditorView.ReloadConfiguration(forceReplaceUnsaved: true);
            ServerSettingsEditorView.ReloadConfiguration(forceReplaceUnsaved: true);
            LogsViewerView.ReloadLogs();
            LiveOutputViewerView.StartMonitoring();
        }
        catch (Exception ex)
        {
            ConnectionsEditorView.ShowStatusMessage($"Startup warning: {ex.Message}");
            ServerSettingsEditorView.ShowStatusMessage($"Startup warning: {ex.Message}");
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

    private void RefreshAll_Click(object sender, RoutedEventArgs e)
    {
        LoadInitialState();
        _ = CheckIpcHandshakeAsync();
    }

    private async Task CheckIpcHandshakeAsync()
    {
        var handshake = await _ipcControlClient.TryHandshakeAsync();
        if (handshake is null)
        {
            DashboardOverviewView.ShowStartupHealth(null);
            ConnectionsEditorView.ShowStatusMessage("IPC control channel unavailable. Service may be offline.");
            _ipcConnected = false;
            UpdateStatusBar();
            return;
        }

        _latestStartupHealth = await _ipcControlClient.TryGetStartupHealthAsync();
        DashboardOverviewView.ShowStartupHealth(_latestStartupHealth);

        _latestConnectionSnapshot = await _ipcControlClient.TryGetConnectionSnapshotAsync();
        DashboardOverviewView.ShowConnectionSnapshot(_latestConnectionSnapshot);

        _latestSecuritySnapshot = await _ipcControlClient.TryGetSecurityBootstrapAsync();
        DashboardOverviewView.ShowSecuritySnapshot(_latestSecuritySnapshot);

        _ipcConnected = true;

        UpdateStatusBar();

        ConnectionsEditorView.ShowStatusMessage(
            $"IPC connected. Protocol {handshake.ProtocolVersion}, service {handshake.ServiceVersion}.");
    }

    private async Task RefreshSnapshotsAsync()
    {
        if (!_ipcConnected)
        {
            await CheckIpcHandshakeAsync();
            return;
        }

        _latestStartupHealth = await _ipcControlClient.TryGetStartupHealthAsync();
        _latestConnectionSnapshot = await _ipcControlClient.TryGetConnectionSnapshotAsync();
        _latestSecuritySnapshot = await _ipcControlClient.TryGetSecurityBootstrapAsync();

        DashboardOverviewView.ShowStartupHealth(_latestStartupHealth);
        DashboardOverviewView.ShowConnectionSnapshot(_latestConnectionSnapshot);
        DashboardOverviewView.ShowSecuritySnapshot(_latestSecuritySnapshot);

        UpdateStatusBar();
    }

    private void LiveOutputViewerView_StreamConnectionStateChanged()
    {
        UpdateStatusBar();
    }

    private void UpdateStatusBar()
    {
        var status = EvaluateServiceStatus();

        if (!_ipcConnected)
        {
            StatusServiceText.Text = status.Text;
            StatusIpcText.Text = "UI-Service: Disconnected";
            StatusClientsText.Text = "Clients connected: n/a";
            StatusServersText.Text = "Servers connected: 0/0";
            StatusFailuresText.Text = "Failures: 0";
            StatusUpdatedText.Text = "Updated: waiting for service";
            SetStatusActions(status.PrimaryAction, status.SecondaryAction);
            return;
        }

        StatusServiceText.Text = status.Text;
        StatusIpcText.Text = "UI-Service: Connected";

        // Local client session count is not published yet by the service.
        StatusClientsText.Text = "Clients connected: n/a";

        var connectedServers = _latestConnectionSnapshot?.Snapshot?.ConnectedEndpointCount ?? 0;
        var enabledServers = _latestConnectionSnapshot?.Snapshot?.EnabledEndpointCount ?? 0;
        var totalFailures = _latestConnectionSnapshot?.Snapshot?.TotalFailureCount ?? 0;

        StatusServersText.Text = $"Servers connected: {connectedServers}/{enabledServers}";
        StatusFailuresText.Text = $"Failures: {totalFailures}";

        var updatedUtc = _latestConnectionSnapshot?.Snapshot?.UpdatedUtc ?? _latestStartupHealth?.Snapshot?.UpdatedUtc;
        StatusUpdatedText.Text = updatedUtc is null
            ? "Updated: n/a"
            : $"Updated: {updatedUtc.Value:HH:mm:ss} UTC";

        SetStatusActions(status.PrimaryAction, status.SecondaryAction);
    }

    private ServiceStatusEvaluation EvaluateServiceStatus()
    {
        if (!_ipcConnected)
        {
            return new ServiceStatusEvaluation(
                "Service Status: Offline (IPC control channel unavailable)",
                StatusAction.RetryIpc,
                StatusAction.OpenLogs);
        }

        if (_latestStartupHealth?.Snapshot?.Status == StartupHealthStatus.Faulted)
        {
            var reason = BuildStartupReasonSuffix(_latestStartupHealth.Snapshot.Reason, fallback: "Service startup failed");
            return new ServiceStatusEvaluation(
                $"Service Status: Failed ({reason})",
                StatusAction.OpenLogs,
                StatusAction.RetryIpc);
        }

        if (_latestStartupHealth?.Snapshot?.Status == StartupHealthStatus.Degraded)
        {
            var reason = BuildStartupReasonSuffix(_latestStartupHealth.Snapshot.Reason, fallback: "Startup validation warnings");
            return new ServiceStatusEvaluation(
                $"Service Status: Limited ({reason})",
                StatusAction.OpenLogs,
                StatusAction.Refresh);
        }

        if (_latestSecuritySnapshot is null || !_latestSecuritySnapshot.Available || _latestSecuritySnapshot.Snapshot is null)
        {
            return new ServiceStatusEvaluation(
                "Service Status: Limited (No security snapshot)",
                StatusAction.OpenLogs,
                StatusAction.Refresh);
        }

        if (_latestSecuritySnapshot.Snapshot.Status == SecurityBootstrapStatus.Faulted)
        {
            return new ServiceStatusEvaluation(
                "Service Status: Failed (Security bootstrap faulted)",
                StatusAction.OpenLogs,
                StatusAction.RetryIpc);
        }

        if (_latestSecuritySnapshot.Snapshot.Status == SecurityBootstrapStatus.Degraded)
        {
            return new ServiceStatusEvaluation(
                "Service Status: Limited (Security trust configuration needs attention)",
                StatusAction.OpenLogs,
                StatusAction.Refresh);
        }

        if (_latestConnectionSnapshot?.Snapshot is null)
        {
            return new ServiceStatusEvaluation(
                "Service Status: Limited (Snapshot refresh pending)",
                StatusAction.Refresh,
                StatusAction.OpenLogs);
        }

        var connected = _latestConnectionSnapshot.Snapshot.ConnectedEndpointCount;
        var enabled = _latestConnectionSnapshot.Snapshot.EnabledEndpointCount;

        if (connected < enabled)
        {
            return new ServiceStatusEvaluation(
                $"Service Status: Limited (Partial upstream connectivity {connected}/{enabled} connected)",
                StatusAction.OpenConnections,
                StatusAction.Refresh);
        }

        if (LiveOutputViewerView.IsEventStreamConnectionKnown && !LiveOutputViewerView.IsEventStreamConnected)
        {
            return new ServiceStatusEvaluation(
                "Service Status: Limited (Live event stream disconnected)",
                StatusAction.RetryIpc,
                StatusAction.Refresh);
        }

        return new ServiceStatusEvaluation(
            "Service Status: Connected (All monitored services nominal)",
            StatusAction.None,
            StatusAction.None);
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

    private void SetStatusActions(StatusAction primary, StatusAction secondary)
    {
        _primaryStatusAction = primary;
        _secondaryStatusAction = secondary;

        ConfigureStatusActionButton(StatusPrimaryActionButton, primary);
        ConfigureStatusActionButton(StatusSecondaryActionButton, secondary);

        StatusActionsPanel.Visibility =
            primary == StatusAction.None && secondary == StatusAction.None
                ? Visibility.Collapsed
                : Visibility.Visible;
    }

    private static void ConfigureStatusActionButton(Button button, StatusAction action)
    {
        if (action == StatusAction.None)
        {
            button.Visibility = Visibility.Collapsed;
            return;
        }

        button.Visibility = Visibility.Visible;
        button.Content = action switch
        {
            StatusAction.RetryIpc => "Retry IPC",
            StatusAction.Refresh => "Refresh",
            StatusAction.OpenLogs => "Open Logs",
            StatusAction.OpenConnections => "Open Connections",
            _ => "Action",
        };
    }

    private async void StatusPrimaryActionButton_Click(object sender, RoutedEventArgs e)
    {
        await ExecuteStatusActionAsync(_primaryStatusAction);
    }

    private async void StatusSecondaryActionButton_Click(object sender, RoutedEventArgs e)
    {
        await ExecuteStatusActionAsync(_secondaryStatusAction);
    }

    private async Task ExecuteStatusActionAsync(StatusAction action)
    {
        if (action == StatusAction.None)
        {
            return;
        }

        StatusPrimaryActionButton.IsEnabled = false;
        StatusSecondaryActionButton.IsEnabled = false;

        try
        {
            switch (action)
            {
                case StatusAction.RetryIpc:
                    await CheckIpcHandshakeAsync();
                    break;
                case StatusAction.Refresh:
                    await RefreshSnapshotsAsync();
                    break;
                case StatusAction.OpenLogs:
                    MainTabs.SelectedItem = LogsTab;
                    break;
                case StatusAction.OpenConnections:
                    MainTabs.SelectedItem = ConnectionsTab;
                    break;
            }
        }
        finally
        {
            await Task.Delay(1000);
            StatusPrimaryActionButton.IsEnabled = true;
            StatusSecondaryActionButton.IsEnabled = true;
        }
    }

    private void ThemeSystem_Click(object sender, RoutedEventArgs e)
    {
        ApplyTheme(ElementTheme.Default);
    }

    private void ThemeLight_Click(object sender, RoutedEventArgs e)
    {
        ApplyTheme(ElementTheme.Light);
    }

    private void ThemeDark_Click(object sender, RoutedEventArgs e)
    {
        ApplyTheme(ElementTheme.Dark);
    }

    private void PaletteWinUI_Click(object sender, RoutedEventArgs e)
    {
        _selectedPalette = ShellPalette.WinUI;
        ApplyShellPalette();
    }

    private void PaletteVSCode_Click(object sender, RoutedEventArgs e)
    {
        _selectedPalette = ShellPalette.VSCode;
        ApplyShellPalette();
    }

    private async void DocumentationHelp_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_helpWindow is null)
            {
                _helpWindow = new HelpWindow();
                _helpWindow.Closed += HelpWindow_Closed;
            }

            _helpWindow.Activate();
        }
        catch (Exception ex)
        {
            await ShowHelpDialogAsync(
                "Unable to open help",
                $"Failed to open help center.\n\n{ex.Message}");
        }
    }

    private void HelpWindow_Closed(object sender, WindowEventArgs args)
    {
        if (_helpWindow is not null)
        {
            _helpWindow.Closed -= HelpWindow_Closed;
            _helpWindow = null;
        }
    }

    private async Task ShowHelpDialogAsync(string title, string content)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = RootLayout.XamlRoot,
            Title = title,
            Content = content,
            PrimaryButtonText = "OK",
            DefaultButton = ContentDialogButton.Primary,
        };

        await dialog.ShowAsync();
    }

    private void ApplyTheme(ElementTheme theme)
    {
        _selectedTheme = theme;

        if (RootLayout is FrameworkElement root)
        {
            root.RequestedTheme = theme;
        }

        ApplyShellPalette();
    }

    private void RootLayout_ActualThemeChanged(FrameworkElement sender, object args)
    {
        ApplyShellPalette();
    }

    private void ApplyShellPalette()
    {
        var effectiveTheme = _selectedTheme == ElementTheme.Default
            ? RootLayout.ActualTheme
            : _selectedTheme;

        var palette = ResolvePalette(effectiveTheme, _selectedPalette);

        SetBrushColor("ShellToolbarBackgroundBrush", palette.ToolbarBackground);
        SetBrushColor("ShellTabStripBackgroundBrush", palette.TabStripBackground);
        SetBrushColor("ShellSurfaceBorderBrush", palette.SurfaceBorder);

        SetTabHeaderBrush("TabViewItemHeaderBackground", palette.TabHeaderBackground);
        SetTabHeaderBrush("TabViewItemHeaderBackgroundSelected", palette.TabHeaderBackgroundSelected);
        SetTabHeaderBrush("TabViewItemHeaderBackgroundPointerOver", palette.TabHeaderBackgroundPointerOver);
    }

    private static ShellPaletteColors ResolvePalette(ElementTheme effectiveTheme, ShellPalette palette)
    {
        var isDark = effectiveTheme == ElementTheme.Dark;

        return palette switch
        {
            ShellPalette.VSCode when isDark => new ShellPaletteColors(
                ToolbarBackground: ColorHelper.FromArgb(0xFF, 0x3C, 0x3C, 0x3C),
                TabStripBackground: ColorHelper.FromArgb(0xFF, 0x25, 0x25, 0x26),
                TabHeaderBackground: ColorHelper.FromArgb(0xFF, 0x2D, 0x2D, 0x30),
                TabHeaderBackgroundSelected: ColorHelper.FromArgb(0xFF, 0x1E, 0x1E, 0x1E),
                TabHeaderBackgroundPointerOver: ColorHelper.FromArgb(0xFF, 0x37, 0x37, 0x3D),
                SurfaceBorder: ColorHelper.FromArgb(0xFF, 0x55, 0x55, 0x5A)
            ),
            ShellPalette.VSCode => new ShellPaletteColors(
                ToolbarBackground: ColorHelper.FromArgb(0xFF, 0xE7, 0xE7, 0xE7),
                TabStripBackground: ColorHelper.FromArgb(0xFF, 0xF3, 0xF3, 0xF3),
                TabHeaderBackground: ColorHelper.FromArgb(0xFF, 0xEA, 0xEA, 0xEA),
                TabHeaderBackgroundSelected: ColorHelper.FromArgb(0xFF, 0xFF, 0xFF, 0xFF),
                TabHeaderBackgroundPointerOver: ColorHelper.FromArgb(0xFF, 0xE0, 0xE0, 0xE0),
                SurfaceBorder: ColorHelper.FromArgb(0xFF, 0xC8, 0xC8, 0xC8)
            ),
            _ when isDark => new ShellPaletteColors(
                ToolbarBackground: ColorHelper.FromArgb(0xFF, 0x25, 0x25, 0x2A),
                TabStripBackground: ColorHelper.FromArgb(0xFF, 0x1F, 0x20, 0x24),
                TabHeaderBackground: ColorHelper.FromArgb(0xFF, 0x2A, 0x2C, 0x31),
                TabHeaderBackgroundSelected: ColorHelper.FromArgb(0xFF, 0x3A, 0x3D, 0x45),
                TabHeaderBackgroundPointerOver: ColorHelper.FromArgb(0xFF, 0x34, 0x37, 0x40),
                SurfaceBorder: ColorHelper.FromArgb(0xFF, 0x4A, 0x4E, 0x57)
            ),
            _ => new ShellPaletteColors(
                ToolbarBackground: ColorHelper.FromArgb(0xFF, 0xF3, 0xF3, 0xF3),
                TabStripBackground: ColorHelper.FromArgb(0xFF, 0xEC, 0xEC, 0xEC),
                TabHeaderBackground: ColorHelper.FromArgb(0xFF, 0xF6, 0xF6, 0xF6),
                TabHeaderBackgroundSelected: ColorHelper.FromArgb(0xFF, 0xFF, 0xFF, 0xFF),
                TabHeaderBackgroundPointerOver: ColorHelper.FromArgb(0xFF, 0xE8, 0xE8, 0xE8),
                SurfaceBorder: ColorHelper.FromArgb(0xFF, 0xC8, 0xC8, 0xC8)
            ),
        };
    }

    private void SetBrushColor(string key, Color color)
    {
        if (Application.Current.Resources.TryGetValue(key, out var resource) && resource is SolidColorBrush brush)
        {
            brush.Color = color;
        }
    }

    private void SetTabHeaderBrush(string key, Color color)
    {
        if (MainTabs.Resources.TryGetValue(key, out var resource) && resource is SolidColorBrush brush)
        {
            brush.Color = color;
        }
    }

    private enum ShellPalette
    {
        WinUI,
        VSCode,
    }

    private readonly record struct ShellPaletteColors(
        Color ToolbarBackground,
        Color TabStripBackground,
        Color TabHeaderBackground,
        Color TabHeaderBackgroundSelected,
        Color TabHeaderBackgroundPointerOver,
        Color SurfaceBorder
    );

    private readonly record struct ServiceStatusEvaluation(
        string Text,
        StatusAction PrimaryAction,
        StatusAction SecondaryAction);

    private enum StatusAction
    {
        None,
        RetryIpc,
        Refresh,
        OpenLogs,
        OpenConnections,
    }
}
