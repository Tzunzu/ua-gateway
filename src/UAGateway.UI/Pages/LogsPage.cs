using Microsoft.UI.Xaml.Controls;
using UAGateway.UI.Controls;

namespace UAGateway.UI.Pages;

public sealed class LogsPage : Page
{
    public LogsViewer View { get; } = new();

    public LogsPage()
    {
        Content = View;
    }
}