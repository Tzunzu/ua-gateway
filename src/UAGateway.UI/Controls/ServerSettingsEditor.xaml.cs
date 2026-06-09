using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using UAGateway.Core.Configuration;

namespace UAGateway.UI.Controls;

public sealed partial class ServerSettingsEditor : UserControl
{
    private string _lastLoadedConfigurationHash = string.Empty;
    private bool _operationInProgress;

    public ServerSettingsEditor()
    {
        InitializeComponent();
    }

    public void ReloadConfiguration(bool forceReplaceUnsaved = false)
    {
        if (_operationInProgress)
        {
            return;
        }

        if (!forceReplaceUnsaved && HasUnsavedChanges())
        {
            _ = PromptAndReloadAsync();
            return;
        }

        ReloadConfigurationCore();
    }

    public void ShowStatusMessage(string message)
    {
        ServerSettingsStatusText.Text = message;
    }

    private async Task PromptAndReloadAsync()
    {
        var prompt = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Unsaved server settings",
            Content = "Reload will replace the current unsaved server settings with values from disk.",
            PrimaryButtonText = "Reload and Replace",
            CloseButtonText = "Keep Editing",
            DefaultButton = ContentDialogButton.Close,
        };

        var result = await prompt.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            ReloadConfigurationCore();
            ServerSettingsStatusText.Text = "Server settings reloaded. Unsaved local edits were replaced.";
        }
        else
        {
            ServerSettingsStatusText.Text = "Reload canceled. Local edits were preserved.";
        }
    }

    private void ReloadConfigurationCore()
    {
        var document = LocalServerConfigurationStore.LoadOrCreateDefault();
        ServerHostTextBox.Text = document.Host;
        ServerPortTextBox.Text = document.Port.ToString(CultureInfo.InvariantCulture);
        ServerEndpointPathTextBox.Text = document.EndpointPath;
        ServerApplicationNameTextBox.Text = document.ApplicationName;
        ServerProductUriTextBox.Text = document.ProductUri;
        SelectComboBoxItemByContent(ServerSecurityModeComboBox, document.SecurityMode);
        SelectComboBoxItemByContent(ServerSecurityPolicyComboBox, document.SecurityPolicy);
        ServerAllowAnonymousToggle.IsOn = document.AllowAnonymous;
        ServerAllowUsernamePasswordToggle.IsOn = document.AllowUsernamePassword;
        _lastLoadedConfigurationHash = ComputeConfigurationHash(document);
        UpdateEndpointPreview();
        ServerSettingsFilePathText.Text = $"Configuration file: {LocalServerConfigurationStore.ConfigurationFilePath}";
        ServerSettingsStatusText.Text = "Server settings reloaded from disk.";
    }

    private void ReloadServerSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        ReloadConfiguration();
    }

    private void RestoreServerSettingsDefaultsButton_Click(object sender, RoutedEventArgs e)
    {
        var defaults = new LocalServerConfigurationDocument();
        ServerHostTextBox.Text = defaults.Host;
        ServerPortTextBox.Text = defaults.Port.ToString(CultureInfo.InvariantCulture);
        ServerEndpointPathTextBox.Text = defaults.EndpointPath;
        ServerApplicationNameTextBox.Text = defaults.ApplicationName;
        ServerProductUriTextBox.Text = defaults.ProductUri;
        SelectComboBoxItemByContent(ServerSecurityModeComboBox, defaults.SecurityMode);
        SelectComboBoxItemByContent(ServerSecurityPolicyComboBox, defaults.SecurityPolicy);
        ServerAllowAnonymousToggle.IsOn = defaults.AllowAnonymous;
        ServerAllowUsernamePasswordToggle.IsOn = defaults.AllowUsernamePassword;
        UpdateEndpointPreview();
        ServerSettingsStatusText.Text = "Defaults restored in editor. Apply Configuration to persist changes.";
    }

    private void ApplyServerSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        if (_operationInProgress)
        {
            return;
        }

        SetOperationInProgress(true);

        if (!TryCreateDraft(out var document, out var errorMessage))
        {
            ServerSettingsStatusText.Text = errorMessage;
            SetOperationInProgress(false);
            return;
        }

        var issues = LocalServerConfigurationValidator.Validate(document);
        if (issues.Count > 0)
        {
            ServerSettingsStatusText.Text = issues[0].Message;
            SetOperationInProgress(false);
            return;
        }

        LocalServerConfigurationStore.Save(document);
        _lastLoadedConfigurationHash = ComputeConfigurationHash(document);
        UpdateEndpointPreview();
        ServerSettingsStatusText.Text = $"Server settings saved. Restart the service to bind {document.BuildBaseAddress()}.";
        SetOperationInProgress(false);
    }

    private void ServerPortTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateEndpointPreview();
    }

    private void ServerHostTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateEndpointPreview();
    }

    private void ServerEndpointPathTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateEndpointPreview();
    }

    private void ServerApplicationNameTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateEndpointPreview();
    }

    private void ServerProductUriTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateEndpointPreview();
    }

    private void UpdateEndpointPreview()
    {
        if (TryCreateDraft(out var document, out _))
        {
            ServerEndpointPreviewText.Text = $"Endpoint preview: {document.BuildBaseAddress()}";
            return;
        }

        ServerEndpointPreviewText.Text = "Endpoint preview: enter a valid host, TCP port, and endpoint path to generate the local OPC UA endpoint address.";
    }

    private bool HasUnsavedChanges()
    {
        return !string.Equals(_lastLoadedConfigurationHash, ComputeEditorHash(), StringComparison.Ordinal);
    }

    private bool TryCreateDraft(out LocalServerConfigurationDocument document, out string errorMessage)
    {
        var host = (ServerHostTextBox.Text ?? string.Empty).Trim();
        var portText = (ServerPortTextBox.Text ?? string.Empty).Trim();
        var endpointPath = (ServerEndpointPathTextBox.Text ?? string.Empty).Trim();
        var applicationName = (ServerApplicationNameTextBox.Text ?? string.Empty).Trim();
        var productUri = (ServerProductUriTextBox.Text ?? string.Empty).Trim();
        var securityMode = GetSelectedComboBoxValue(ServerSecurityModeComboBox, "SignAndEncrypt");
        var securityPolicy = GetSelectedComboBoxValue(ServerSecurityPolicyComboBox, "Basic256Sha256");
        var allowAnonymous = ServerAllowAnonymousToggle.IsOn;
        var allowUsernamePassword = ServerAllowUsernamePasswordToggle.IsOn;

        if (host.Length == 0)
        {
            document = new LocalServerConfigurationDocument();
            errorMessage = "Apply failed. Local server host is required.";
            return false;
        }

        if (!int.TryParse(portText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var port))
        {
            document = new LocalServerConfigurationDocument();
            errorMessage = "Apply failed. Local server port must be a whole number between 1 and 65535.";
            return false;
        }

        if (LocalServerConfigurationDocument.NormalizeEndpointPath(endpointPath).Length == 0)
        {
            document = new LocalServerConfigurationDocument();
            errorMessage = "Apply failed. Endpoint path is required.";
            return false;
        }

        if (applicationName.Length == 0)
        {
            document = new LocalServerConfigurationDocument();
            errorMessage = "Apply failed. Application name is required.";
            return false;
        }

        if (productUri.Length == 0)
        {
            document = new LocalServerConfigurationDocument();
            errorMessage = "Apply failed. Product URI is required.";
            return false;
        }

        document = new LocalServerConfigurationDocument
        {
            Host = host,
            Port = port,
            EndpointPath = endpointPath,
            ApplicationName = applicationName,
            ProductUri = productUri,
            SecurityMode = securityMode,
            SecurityPolicy = securityPolicy,
            AllowAnonymous = allowAnonymous,
            AllowUsernamePassword = allowUsernamePassword,
        };
        errorMessage = string.Empty;
        return true;
    }

    private string ComputeEditorHash()
    {
        return TryCreateDraft(out var document, out _)
            ? ComputeConfigurationHash(document)
            : $"invalid:{(ServerHostTextBox.Text ?? string.Empty).Trim()}:{(ServerPortTextBox.Text ?? string.Empty).Trim()}:{(ServerEndpointPathTextBox.Text ?? string.Empty).Trim()}:{(ServerApplicationNameTextBox.Text ?? string.Empty).Trim()}:{(ServerProductUriTextBox.Text ?? string.Empty).Trim()}:{GetSelectedComboBoxValue(ServerSecurityModeComboBox, "SignAndEncrypt")}:{GetSelectedComboBoxValue(ServerSecurityPolicyComboBox, "Basic256Sha256")}:{ServerAllowAnonymousToggle.IsOn}:{ServerAllowUsernamePasswordToggle.IsOn}";
    }

    private static string ComputeConfigurationHash(LocalServerConfigurationDocument document)
    {
        return JsonSerializer.Serialize(document);
    }

    private void SetOperationInProgress(bool value)
    {
        _operationInProgress = value;
        ApplyServerSettingsButton.IsEnabled = !value;
        ReloadServerSettingsButton.IsEnabled = !value;
        RestoreServerSettingsDefaultsButton.IsEnabled = !value;
    }

    private static void SelectComboBoxItemByContent(ComboBox comboBox, string? value)
    {
        foreach (var item in comboBox.Items.OfType<ComboBoxItem>())
        {
            if (string.Equals(item.Content as string, value, StringComparison.OrdinalIgnoreCase))
            {
                comboBox.SelectedItem = item;
                return;
            }
        }

        comboBox.SelectedIndex = 0;
    }

    private static string GetSelectedComboBoxValue(ComboBox comboBox, string fallback)
    {
        if (comboBox.SelectedItem is ComboBoxItem selected && selected.Content is string value && !string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return fallback;
    }
}