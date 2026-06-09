using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace UAGateway.UI.Controls;

public sealed partial class SettingsFieldBox : UserControl
{
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(
        nameof(Label),
        typeof(string),
        typeof(SettingsFieldBox),
        new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty FieldWidthProperty = DependencyProperty.Register(
        nameof(FieldWidth),
        typeof(double),
        typeof(SettingsFieldBox),
        new PropertyMetadata(220d));

    public static readonly DependencyProperty FieldContentProperty = DependencyProperty.Register(
        nameof(FieldContent),
        typeof(object),
        typeof(SettingsFieldBox),
        new PropertyMetadata(null));

    public SettingsFieldBox()
    {
        InitializeComponent();
    }

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public double FieldWidth
    {
        get => (double)GetValue(FieldWidthProperty);
        set => SetValue(FieldWidthProperty, value);
    }

    public object? FieldContent
    {
        get => GetValue(FieldContentProperty);
        set => SetValue(FieldContentProperty, value);
    }
}
