using Microsoft.UI.Xaml.Controls;
using UAGateway.UI.Controls;

namespace UAGateway.UI.Pages;

public sealed class LiveOutputPage : Page
{
    public LiveOutputViewer View { get; } = new();

    public LiveOutputPage()
    {
        Content = View;
    }
}