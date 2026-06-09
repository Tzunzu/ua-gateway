using Microsoft.UI.Xaml.Controls;
using UAGateway.UI.Pages;

namespace UAGateway.UI.Services;

public sealed class ShellNavigationCoordinator
{
    private readonly Dictionary<string, RouteEntry> _routes = new(StringComparer.Ordinal)
    {
        [ShellRouteKeys.Dashboard] = new(ShellRouteKeys.Dashboard, "Dashboard", () => new DashboardPage()),
        [ShellRouteKeys.Connections] = new(ShellRouteKeys.Connections, "Connections", () => new ConnectionsPage()),
        [ShellRouteKeys.ServerSettings] = new(ShellRouteKeys.ServerSettings, "Server Settings", () => new ServerSettingsPage()),
        [ShellRouteKeys.Logs] = new(ShellRouteKeys.Logs, "Logs", () => new LogsPage()),
        [ShellRouteKeys.LiveOutput] = new(ShellRouteKeys.LiveOutput, "Live Output", () => new LiveOutputPage()),
        [ShellRouteKeys.Settings] = new(ShellRouteKeys.Settings, "Settings", () => new SettingsPage()),
    };

    private readonly Dictionary<string, Page> _pageCache = new(StringComparer.Ordinal);

    public bool TryGetRoute(string routeKey, out string title)
    {
        if (_routes.TryGetValue(routeKey, out var route))
        {
            title = route.Title;
            return true;
        }

        title = string.Empty;
        return false;
    }

    public TPage GetPage<TPage>(string routeKey)
        where TPage : Page
    {
        return (TPage)GetPage(routeKey);
    }

    public Page GetPage(string routeKey)
    {
        if (!_routes.TryGetValue(routeKey, out var route))
        {
            route = _routes[ShellRouteKeys.Dashboard];
        }

        if (_pageCache.TryGetValue(route.Key, out var cached))
        {
            return cached;
        }

        var page = route.CreatePage();
        _pageCache[route.Key] = page;
        return page;
    }

    private sealed record RouteEntry(string Key, string Title, Func<Page> CreatePage);
}
