using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Windowing;
using UAGateway.Core.Ipc;
using UAGateway.UI.Services;
using Windows.UI;
using Windows.Graphics;
using WinRT.Interop;

namespace UAGateway.UI;

public sealed partial class MainWindow : Window
{
    private readonly IpcControlClient _ipcControlClient = new();
    private ElementTheme _selectedTheme = ElementTheme.Dark;
    private ShellPalette _selectedPalette = ShellPalette.WinUI;

    public MainWindow()
    {
        InitializeComponent();

        RootLayout.ActualThemeChanged += RootLayout_ActualThemeChanged;
        ApplyTheme(_selectedTheme);
        ApplyShellPalette();

        EnsureVisibleOnLaunch();
        LoadInitialState();
        _ = CheckIpcHandshakeAsync();
    }

    private void LoadInitialState()
    {
        UpdateStatusBar(null, null, ipcConnected: false);

        try
        {
            DashboardOverviewView.RefreshDiagnostics();
            DashboardOverviewView.ShowStartupHealth(null);
            ConnectionsEditorView.ReloadDraft();
            LogsViewerView.ReloadLogs();
            LiveOutputViewerView.StartMonitoring();
        }
        catch (Exception ex)
        {
            ConnectionsEditorView.ShowStatusMessage($"Startup warning: {ex.Message}");
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
            UpdateStatusBar(null, null, ipcConnected: false);
            return;
        }

        var startupHealth = await _ipcControlClient.TryGetStartupHealthAsync();
        DashboardOverviewView.ShowStartupHealth(startupHealth);

        var connectionSnapshot = await _ipcControlClient.TryGetConnectionSnapshotAsync();
        DashboardOverviewView.ShowConnectionSnapshot(connectionSnapshot);

        var securitySnapshot = await _ipcControlClient.TryGetSecurityBootstrapAsync();
        DashboardOverviewView.ShowSecuritySnapshot(securitySnapshot);

        UpdateStatusBar(startupHealth, connectionSnapshot, ipcConnected: true);

        ConnectionsEditorView.ShowStatusMessage(
            $"IPC connected. Protocol {handshake.ProtocolVersion}, service {handshake.ServiceVersion}.");
    }

    private void UpdateStatusBar(
        IpcStartupHealthSnapshotPayload? startupHealth,
        IpcConnectionSnapshotPayload? connectionSnapshot,
        bool ipcConnected)
    {
        if (!ipcConnected)
        {
            StatusServiceText.Text = "Service: Offline";
            StatusIpcText.Text = "UI-Service: Disconnected";
            StatusClientsText.Text = "Clients connected: n/a";
            StatusServersText.Text = "Servers connected: 0/0";
            StatusFailuresText.Text = "Failures: 0";
            StatusUpdatedText.Text = "Updated: waiting for service";
            return;
        }

        var startupStatus = startupHealth?.Snapshot?.Status.ToString() ?? "Unknown";
        StatusServiceText.Text = $"Service: {startupStatus}";
        StatusIpcText.Text = "UI-Service: Connected";

        // Local client session count is not published yet by the service.
        StatusClientsText.Text = "Clients connected: n/a";

        var connectedServers = connectionSnapshot?.Snapshot?.ConnectedEndpointCount ?? 0;
        var enabledServers = connectionSnapshot?.Snapshot?.EnabledEndpointCount ?? 0;
        var totalFailures = connectionSnapshot?.Snapshot?.TotalFailureCount ?? 0;

        StatusServersText.Text = $"Servers connected: {connectedServers}/{enabledServers}";
        StatusFailuresText.Text = $"Failures: {totalFailures}";

        var updatedUtc = connectionSnapshot?.Snapshot?.UpdatedUtc ?? startupHealth?.Snapshot?.UpdatedUtc;
        StatusUpdatedText.Text = updatedUtc is null
            ? "Updated: n/a"
            : $"Updated: {updatedUtc.Value:HH:mm:ss} UTC";
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
}
