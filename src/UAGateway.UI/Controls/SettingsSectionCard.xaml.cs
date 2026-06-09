using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace UAGateway.UI.Controls;

public sealed partial class SettingsSectionCard : UserControl
{
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title),
        typeof(string),
        typeof(SettingsSectionCard),
        new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty SectionContentProperty = DependencyProperty.Register(
        nameof(SectionContent),
        typeof(object),
        typeof(SettingsSectionCard),
        new PropertyMetadata(null));

    public SettingsSectionCard()
    {
        InitializeComponent();
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public object? SectionContent
    {
        get => GetValue(SectionContentProperty);
        set => SetValue(SectionContentProperty, value);
    }
}
