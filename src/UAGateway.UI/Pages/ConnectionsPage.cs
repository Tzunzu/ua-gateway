using Microsoft.UI.Xaml.Controls;
using UAGateway.UI.Controls;

namespace UAGateway.UI.Pages;

public sealed class ConnectionsPage : Page
{
    public ConnectionsEditor View { get; } = new();

    public ConnectionsPage()
    {
        Content = View;
    }
}