using Microsoft.UI.Xaml.Controls;
using UAGateway.UI.Controls;

namespace UAGateway.UI.Pages;

public sealed class DashboardPage : Page
{
    public DashboardOverview View { get; } = new();

    public DashboardPage()
    {
        Content = View;
    }
}