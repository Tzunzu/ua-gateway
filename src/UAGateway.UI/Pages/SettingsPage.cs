using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace UAGateway.UI.Pages;

public sealed class SettingsPage : Page
{
    private readonly ComboBox _themeComboBox;
    private readonly ComboBox _paletteComboBox;
    private bool _isUpdatingSelections;

    public event Action<string>? ThemeSelectionRequested;

    public event Action<string>? PaletteSelectionRequested;

    public event Action? HelpRequested;

    public SettingsPage()
    {
        _themeComboBox = new ComboBox
        {
            Width = 220,
            ItemsSource = new[] { "System", "Light", "Dark" },
        };
        _themeComboBox.SelectionChanged += ThemeComboBox_SelectionChanged;

        _paletteComboBox = new ComboBox
        {
            Width = 220,
            ItemsSource = new[] { "WinUI", "VSCode" },
        };
        _paletteComboBox.SelectionChanged += PaletteComboBox_SelectionChanged;

        var openHelpButton = new Button
        {
            Content = "Open Help Center",
        };
        openHelpButton.Click += (_, _) => HelpRequested?.Invoke();

        var aboutCommandsButton = new Button
        {
            Content = "About Shell Commands",
        };
        aboutCommandsButton.Click += (_, _) => ShowCommandInfoDialog();

        var layout = new StackPanel
        {
            Spacing = 16,
            Children =
            {
                new TextBlock
                {
                    Text = "Settings",
                    FontSize = 24,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                },
                new TextBlock
                {
                    Text = "Theme, shell palette, and documentation commands are hosted here for operator workflows.",
                    TextWrapping = TextWrapping.WrapWholeWords,
                },

                BuildSelectionRow("Theme", _themeComboBox),
                BuildSelectionRow("Palette", _paletteComboBox),

                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 10,
                    Children =
                    {
                        openHelpButton,
                        aboutCommandsButton,
                    },
                },
                new TextBlock
                {
                    Text = "Command mapping: Theme/Palette moved from old menu to Settings. Help remains available in header and here.",
                    TextWrapping = TextWrapping.WrapWholeWords,
                },
            },
            Padding = new Thickness(0, 8, 0, 0),
        };

        Content = layout;

        SetSelectedTheme("Dark");
        SetSelectedPalette("WinUI");
    }

    public void SetSelectedTheme(string theme)
    {
        _isUpdatingSelections = true;
        _themeComboBox.SelectedItem = theme;
        _isUpdatingSelections = false;
    }

    public void SetSelectedPalette(string palette)
    {
        _isUpdatingSelections = true;
        _paletteComboBox.SelectedItem = palette;
        _isUpdatingSelections = false;
    }

    private static StackPanel BuildSelectionRow(string label, ComboBox comboBox)
    {
        return new StackPanel
        {
            Spacing = 6,
            Children =
            {
                new TextBlock
                {
                    Text = label,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                },
                comboBox,
            },
        };
    }

    private void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingSelections)
        {
            return;
        }

        if (_themeComboBox.SelectedItem is string selected)
        {
            ThemeSelectionRequested?.Invoke(selected);
        }
    }

    private void PaletteComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingSelections)
        {
            return;
        }

        if (_paletteComboBox.SelectedItem is string selected)
        {
            PaletteSelectionRequested?.Invoke(selected);
        }
    }

    private async void ShowCommandInfoDialog()
    {
        if (XamlRoot is null)
        {
            return;
        }

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Shell command placement",
            Content = "Theme and palette are managed in Settings. Refresh and Help are available from the shell header on all routes.",
            PrimaryButtonText = "OK",
            DefaultButton = ContentDialogButton.Primary,
        };

        await dialog.ShowAsync();
    }
}