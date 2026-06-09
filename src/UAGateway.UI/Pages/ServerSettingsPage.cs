using Microsoft.UI.Xaml.Controls;
using UAGateway.UI.Controls;

namespace UAGateway.UI.Pages;

public sealed class ServerSettingsPage : Page
{
    public ServerSettingsEditor View { get; } = new();

    public ServerSettingsPage()
    {
        Content = View;
    }
}