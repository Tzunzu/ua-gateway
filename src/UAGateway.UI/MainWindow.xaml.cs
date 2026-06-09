using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Windowing;
using UAGateway.UI.Pages;
using UAGateway.UI.Services;
using System.IO;
using System.Linq;
using Windows.UI;
using Windows.Graphics;
using WinRT.Interop;

namespace UAGateway.UI;

public sealed partial class MainWindow : Window
{
    private readonly ShellStateService _shellStateService = new();
    private readonly ShellNavigationCoordinator _navigationCoordinator = new();
    private readonly DashboardPage _dashboardPage;
    private readonly ConnectionsPage _connectionsPage;
    private readonly ServerSettingsPage _serverSettingsPage;
    private readonly LogsPage _logsPage;
    private readonly LiveOutputPage _liveOutputPage;
    private readonly SettingsPage _settingsPage;
    private readonly Stack<string> _routeBackStack = new();
    private HelpWindow? _helpWindow;
    private bool _isClosing;
    private ElementTheme _selectedTheme = ElementTheme.Dark;
    private ShellPalette _selectedPalette = ShellPalette.WinUI;
    private string _currentRoute = string.Empty;

    public MainWindow()
    {
        _dashboardPage = _navigationCoordinator.GetPage<DashboardPage>(ShellRouteKeys.Dashboard);
        _connectionsPage = _navigationCoordinator.GetPage<ConnectionsPage>(ShellRouteKeys.Connections);
        _serverSettingsPage = _navigationCoordinator.GetPage<ServerSettingsPage>(ShellRouteKeys.ServerSettings);
        _logsPage = _navigationCoordinator.GetPage<LogsPage>(ShellRouteKeys.Logs);
        _liveOutputPage = _navigationCoordinator.GetPage<LiveOutputPage>(ShellRouteKeys.LiveOutput);
        _settingsPage = _navigationCoordinator.GetPage<SettingsPage>(ShellRouteKeys.Settings);

        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        ConfigureWindowTitleBar();
        Closed += MainWindow_Closed;

        RootLayout.ActualThemeChanged += RootLayout_ActualThemeChanged;
        _liveOutputPage.View.StreamConnectionStateChanged += LiveOutputViewerView_StreamConnectionStateChanged;
        _settingsPage.ThemeSelectionRequested += SettingsPage_ThemeSelectionRequested;
        _settingsPage.PaletteSelectionRequested += SettingsPage_PaletteSelectionRequested;
        _settingsPage.HelpRequested += SettingsPage_HelpRequested;
        ApplyTheme(_selectedTheme);
        ApplyShellPalette();

        _settingsPage.SetSelectedTheme(MapThemeToSelection(_selectedTheme));
        _settingsPage.SetSelectedPalette(MapPaletteToSelection(_selectedPalette));

        EnsureVisibleOnLaunch();
        ApplyWindowIcon();
        LoadInitialState();
        NavigateToRoute(ShellRouteKeys.Dashboard, updateSelection: true);
        _ = CheckIpcHandshakeAsync();
    }

    private void LoadInitialState()
    {
        _shellStateService.Reset();
        UpdateStatusBar();

        try
        {
            _dashboardPage.View.RefreshDiagnostics();
            _dashboardPage.View.ShowStartupHealth(null);
            _connectionsPage.View.ReloadConfiguration(forceReplaceUnsaved: true);
            _serverSettingsPage.View.ReloadConfiguration(forceReplaceUnsaved: true);
            _logsPage.View.ReloadLogs();
            _liveOutputPage.View.StartMonitoring();
        }
        catch (Exception ex)
        {
            _connectionsPage.View.ShowStatusMessage($"Startup warning: {ex.Message}");
            _serverSettingsPage.View.ShowStatusMessage($"Startup warning: {ex.Message}");
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

    private void ApplyWindowIcon()
    {
        try
        {
            var baseDirectory = AppContext.BaseDirectory;
            var iconCandidates = new[]
            {
                Path.Combine(baseDirectory, "Assets", "Brand", "gateway-icon.ico"),
                Path.Combine(baseDirectory, "gateway-icon.ico"),
            };

            var iconPath = iconCandidates.FirstOrDefault(File.Exists);
            if (!string.IsNullOrWhiteSpace(iconPath))
            {
                AppWindow.SetIcon(iconPath);
            }
        }
        catch
        {
            // Best-effort only. Keep launch stable if icon loading fails.
        }
    }

    private void ConfigureWindowTitleBar()
    {
        var titleBar = AppWindow.TitleBar;
        if (!AppWindowTitleBar.IsCustomizationSupported())
        {
            return;
        }

        titleBar.ButtonBackgroundColor = Colors.Transparent;
        titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
        titleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        AppWindow.Changed += AppWindow_Changed;

        UpdateTitleBarInsets();
    }

    private void AppWindow_Changed(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (_isClosing)
        {
            return;
        }

        DispatcherQueue.TryEnqueue(UpdateTitleBarInsets);
    }

    private void UpdateTitleBarInsets()
    {
        var leftInsetColumn = AppTitleBarLeftInsetColumn;
        var rightInsetColumn = AppTitleBarRightInsetColumn;

        if (_isClosing || leftInsetColumn is null || rightInsetColumn is null)
        {
            return;
        }

        try
        {
            if (!AppWindowTitleBar.IsCustomizationSupported())
            {
                leftInsetColumn.Width = new GridLength(0);
                rightInsetColumn.Width = new GridLength(0);
                return;
            }

            var titleBar = AppWindow?.TitleBar;
            if (titleBar is null)
            {
                leftInsetColumn.Width = new GridLength(0);
                rightInsetColumn.Width = new GridLength(0);
                return;
            }

            leftInsetColumn.Width = new GridLength(titleBar.LeftInset);
            rightInsetColumn.Width = new GridLength(titleBar.RightInset);
        }
        catch
        {
            // Keep the window stable if the title bar becomes unavailable during shutdown/transition.
            if (leftInsetColumn is not null)
            {
                leftInsetColumn.Width = new GridLength(0);
            }

            if (rightInsetColumn is not null)
            {
                rightInsetColumn.Width = new GridLength(0);
            }
        }
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        _isClosing = true;
        AppWindow.Changed -= AppWindow_Changed;
    }

    private async Task CheckIpcHandshakeAsync()
    {
        var handshake = await _shellStateService.CheckIpcHandshakeAsync();
        if (handshake is null)
        {
            _dashboardPage.View.ShowStartupHealth(null);
            UpdateStatusBar();
            return;
        }

        _dashboardPage.View.ShowStartupHealth(_shellStateService.LatestStartupHealth);
        _dashboardPage.View.ShowConnectionSnapshot(_shellStateService.LatestConnectionSnapshot);
        _dashboardPage.View.ShowSecuritySnapshot(_shellStateService.LatestSecuritySnapshot);

        UpdateStatusBar();
    }

    private async Task RefreshSnapshotsAsync()
    {
        await _shellStateService.RefreshSnapshotsAsync();

        _dashboardPage.View.ShowStartupHealth(_shellStateService.LatestStartupHealth);
        _dashboardPage.View.ShowConnectionSnapshot(_shellStateService.LatestConnectionSnapshot);
        _dashboardPage.View.ShowSecuritySnapshot(_shellStateService.LatestSecuritySnapshot);

        UpdateStatusBar();
    }

    private void LiveOutputViewerView_StreamConnectionStateChanged()
    {
        _shellStateService.SetEventStreamState(
            _liveOutputPage.View.IsEventStreamConnectionKnown,
            _liveOutputPage.View.IsEventStreamConnected);
        UpdateStatusBar();
    }

    private void UpdateStatusBar()
    {
        var status = _shellStateService.EvaluateServiceStatus();

        if (!_shellStateService.IpcConnected)
        {
            StatusServiceText.Text = status.Text;
            StatusClientsText.Text = "Clients connected: n/a";
            StatusServersText.Text = "Servers connected: 0/0";
            StatusFailuresText.Text = "Failures: 0";
            StatusUpdatedText.Text = "Updated: waiting for service";
            return;
        }

        StatusServiceText.Text = status.Text;

        // Local client session count is not published yet by the service.
        StatusClientsText.Text = "Clients connected: n/a";

        var connectedServers = _shellStateService.LatestConnectionSnapshot?.Snapshot?.ConnectedEndpointCount ?? 0;
        var enabledServers = _shellStateService.LatestConnectionSnapshot?.Snapshot?.EnabledEndpointCount ?? 0;
        var totalFailures = _shellStateService.LatestConnectionSnapshot?.Snapshot?.TotalFailureCount ?? 0;

        StatusServersText.Text = $"Servers connected: {connectedServers}/{enabledServers}";
        StatusFailuresText.Text = $"Failures: {totalFailures}";

        var updatedUtc = _shellStateService.LatestConnectionSnapshot?.Snapshot?.UpdatedUtc
            ?? _shellStateService.LatestStartupHealth?.Snapshot?.UpdatedUtc;
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

        _settingsPage.SetSelectedTheme(MapThemeToSelection(_selectedTheme));
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
    }

    private static ShellPaletteColors ResolvePalette(ElementTheme effectiveTheme, ShellPalette palette)
    {
        var isDark = effectiveTheme == ElementTheme.Dark;

        return palette switch
        {
            ShellPalette.VSCode when isDark => new ShellPaletteColors(
                ToolbarBackground: ColorHelper.FromArgb(0xFF, 0x3C, 0x3C, 0x3C),
                TabStripBackground: ColorHelper.FromArgb(0xFF, 0x25, 0x25, 0x26),
                SurfaceBorder: ColorHelper.FromArgb(0xFF, 0x55, 0x55, 0x5A)
            ),
            ShellPalette.VSCode => new ShellPaletteColors(
                ToolbarBackground: ColorHelper.FromArgb(0xFF, 0xE7, 0xE7, 0xE7),
                TabStripBackground: ColorHelper.FromArgb(0xFF, 0xF3, 0xF3, 0xF3),
                SurfaceBorder: ColorHelper.FromArgb(0xFF, 0xC8, 0xC8, 0xC8)
            ),
            _ when isDark => new ShellPaletteColors(
                ToolbarBackground: ColorHelper.FromArgb(0xFF, 0x25, 0x25, 0x2A),
                TabStripBackground: ColorHelper.FromArgb(0xFF, 0x1F, 0x20, 0x24),
                SurfaceBorder: ColorHelper.FromArgb(0xFF, 0x4A, 0x4E, 0x57)
            ),
            _ => new ShellPaletteColors(
                ToolbarBackground: ColorHelper.FromArgb(0xFF, 0xF3, 0xF3, 0xF3),
                TabStripBackground: ColorHelper.FromArgb(0xFF, 0xEC, 0xEC, 0xEC),
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

    private void ShellNavigation_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.IsSettingsSelected)
        {
            NavigateToRoute(ShellRouteKeys.Settings, updateSelection: false);
            return;
        }

        if (args.SelectedItem is NavigationViewItem item && item.Tag is string route)
        {
            NavigateToRoute(route, updateSelection: false);
        }
    }

    private void SettingsPage_ThemeSelectionRequested(string selection)
    {
        var theme = selection switch
        {
            "System" => ElementTheme.Default,
            "Light" => ElementTheme.Light,
            "Dark" => ElementTheme.Dark,
            _ => ElementTheme.Default,
        };

        ApplyTheme(theme);
    }

    private void SettingsPage_PaletteSelectionRequested(string selection)
    {
        _selectedPalette = selection switch
        {
            "VSCode" => ShellPalette.VSCode,
            _ => ShellPalette.WinUI,
        };

        _settingsPage.SetSelectedPalette(MapPaletteToSelection(_selectedPalette));
        ApplyShellPalette();
    }

    private void SettingsPage_HelpRequested()
    {
        DocumentationHelp_Click(this, new RoutedEventArgs());
    }

    private void ShellNavigation_BackRequested(NavigationView sender, NavigationViewBackRequestedEventArgs args)
    {
        if (_routeBackStack.Count == 0)
        {
            return;
        }

        var previousRoute = _routeBackStack.Pop();
        NavigateToRoute(previousRoute, updateSelection: true, preserveBackStack: true);
    }

    private void NavigateToRoute(string route, bool updateSelection, bool preserveBackStack = false)
    {
        if (string.Equals(route, _currentRoute, StringComparison.Ordinal))
        {
            return;
        }

        if (!preserveBackStack && !string.IsNullOrWhiteSpace(_currentRoute))
        {
            _routeBackStack.Push(_currentRoute);
        }

        Page page = _navigationCoordinator.GetPage(route);

        ShellFrame.Content = page;
        _currentRoute = route;

        var showHeader = !string.Equals(route, ShellRouteKeys.Connections, StringComparison.Ordinal);
        ShellNavigation.Header = showHeader ? ShellHeaderContainer : null;
        ShellHeaderText.Visibility = showHeader ? Visibility.Visible : Visibility.Collapsed;

        ShellHeaderText.Text = _navigationCoordinator.TryGetRoute(route, out var title)
            ? title
            : "Dashboard";
        ShellNavigation.IsBackEnabled = _routeBackStack.Count > 0;

        if (updateSelection)
        {
            object selectedItem = route switch
            {
                ShellRouteKeys.Dashboard => DashboardNavItem,
                ShellRouteKeys.Connections => ConnectionsNavItem,
                ShellRouteKeys.ServerSettings => ServerSettingsNavItem,
                ShellRouteKeys.Logs => LogsNavItem,
                ShellRouteKeys.LiveOutput => LiveOutputNavItem,
                ShellRouteKeys.Settings => ShellNavigation.SettingsItem,
                _ => DashboardNavItem,
            };

            ShellNavigation.SelectedItem = selectedItem;
        }
    }

    private enum ShellPalette
    {
        WinUI,
        VSCode,
    }

    private static string MapThemeToSelection(ElementTheme theme)
    {
        return theme switch
        {
            ElementTheme.Light => "Light",
            ElementTheme.Dark => "Dark",
            _ => "System",
        };
    }

    private static string MapPaletteToSelection(ShellPalette palette)
    {
        return palette switch
        {
            ShellPalette.VSCode => "VSCode",
            _ => "WinUI",
        };
    }

    private readonly record struct ShellPaletteColors(
        Color ToolbarBackground,
        Color TabStripBackground,
        Color SurfaceBorder
    );

}
