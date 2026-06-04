using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Text.Json;
using UAGateway.Core.Configuration;

namespace UAGateway.UI.Controls;

public sealed partial class ConnectionsEditor : UserControl
{
    private UpstreamEndpointConfigurationDocument _draftEndpoints = new();
    private string _lastLoadedConfigurationHash = string.Empty;
    private string? _selectedEndpointId;
    private bool _isUpdatingConnectionDetails;
    private bool _operationInProgress;

    public ConnectionsEditor()
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

    public void ReloadDraft()
    {
        ReloadConfiguration();
    }

    private async Task PromptAndReloadAsync()
    {
        var prompt = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Unsaved configuration changes",
            Content = "Reload will replace current unsaved edits with configuration from service.",
            PrimaryButtonText = "Reload and Replace",
            CloseButtonText = "Keep Editing",
            DefaultButton = ContentDialogButton.Close,
        };

        var result = await prompt.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            ReloadConfigurationCore();
            ConnectionsApplyStatusText.Text = "Configuration reloaded. Unsaved local edits were replaced.";
        }
        else
        {
            ConnectionsApplyStatusText.Text = "Reload canceled. Local edits were preserved.";
        }
    }

    private void ReloadConfigurationCore()
    {
        _draftEndpoints = UpstreamEndpointConfigurationStore.LoadOrCreateDefault();
        _lastLoadedConfigurationHash = ComputeConfigurationHash(_draftEndpoints);
        ConnectionsApplyStatusText.Text = "Configuration reloaded from disk.";

        if (_draftEndpoints.Endpoints.All(endpoint => endpoint.Id != _selectedEndpointId))
        {
            _selectedEndpointId = _draftEndpoints.Endpoints.FirstOrDefault()?.Id;
        }

        RenderConnectionsDraft();
    }

    public void ShowStatusMessage(string message)
    {
        ConnectionsApplyStatusText.Text = message;
    }

    private void RenderConnectionsDraft()
    {
        ConnectionsHeaderText.Text = $"Configured endpoints: {_draftEndpoints.Endpoints.Count}";

        ConnectionsList.Items.Clear();

        foreach (var endpoint in _draftEndpoints.Endpoints)
        {
            var enabledFlag = endpoint.Enabled ? "Enabled" : "Disabled";

            var itemContent = new StackPanel
            {
                Spacing = 2,
            };

            itemContent.Children.Add(new TextBlock
            {
                Text = endpoint.DisplayName,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            });
            itemContent.Children.Add(new TextBlock
            {
                Text = endpoint.EndpointUrl,
                Opacity = 0.75,
                TextWrapping = TextWrapping.WrapWholeWords,
            });
            itemContent.Children.Add(new TextBlock
            {
                Text = enabledFlag,
                Opacity = 0.65,
            });

            ConnectionsList.Items.Add(new ListViewItem
            {
                Content = itemContent,
                Tag = endpoint.Id,
            });
        }

        SelectConnectionById(_selectedEndpointId ?? _draftEndpoints.Endpoints.FirstOrDefault()?.Id);
    }

    private void AddConnectionDraft_Click(object sender, RoutedEventArgs e)
    {
        var newEndpoint = new UpstreamEndpointConfiguration
        {
            Id = Guid.NewGuid().ToString("N"),
            DisplayName = $"New Connection {_draftEndpoints.Endpoints.Count + 1}",
            EndpointUrl = string.Empty,
            Enabled = true,
        };

        _draftEndpoints.Endpoints.Add(newEndpoint);
        _selectedEndpointId = newEndpoint.Id;
        RenderConnectionsDraft();
        ConnectionsApplyStatusText.Text = "Endpoint added to configuration. Apply Configuration to persist.";
    }

    private void ReloadConnectionsDraft_Click(object sender, RoutedEventArgs e)
    {
        ReloadConfiguration();
    }

    private void ApplyConnectionsDraft_Click(object sender, RoutedEventArgs e)
    {
        if (_operationInProgress)
        {
            return;
        }

        SetOperationInProgress(true);

        var issues = UpstreamEndpointConfigurationValidator.Validate(_draftEndpoints);
        if (issues.Count > 0)
        {
            var firstIssue = issues[0];
            ConnectionsApplyStatusText.Text =
                $"Apply failed. Validation issues: {issues.Count}. First issue: [{firstIssue.EndpointId}] {firstIssue.Message}";
            SetOperationInProgress(false);
            return;
        }

        UpstreamEndpointConfigurationStore.Save(_draftEndpoints);
        _lastLoadedConfigurationHash = ComputeConfigurationHash(_draftEndpoints);
        ConnectionsApplyStatusText.Text = "Apply succeeded. Configuration saved to upstream endpoint configuration file.";
        RenderConnectionsDraft();
        SetOperationInProgress(false);
    }

    private void ConnectionsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ConnectionsList.SelectedItem is ListViewItem selectedItem)
        {
            _selectedEndpointId = selectedItem.Tag as string;
        }
        else
        {
            _selectedEndpointId = null;
        }

        RenderSelectedConnectionDetails();
    }

    private void DraftDisplayNameTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingConnectionDetails)
        {
            return;
        }

        UpdateSelectedEndpoint(endpoint => endpoint with
        {
            DisplayName = (DraftDisplayNameTextBox.Text ?? string.Empty).Trim(),
        });
    }

    private void DraftEndpointUrlTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingConnectionDetails)
        {
            return;
        }

        UpdateSelectedEndpoint(endpoint => endpoint with
        {
            EndpointUrl = (DraftEndpointUrlTextBox.Text ?? string.Empty).Trim(),
        });
    }

    private void DraftEnabledToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingConnectionDetails)
        {
            return;
        }

        UpdateSelectedEndpoint(endpoint => endpoint with
        {
            Enabled = DraftEnabledToggle.IsOn,
        });
    }

    private void SelectConnectionById(string? endpointId)
    {
        _selectedEndpointId = endpointId;

        if (endpointId is null)
        {
            ConnectionsList.SelectedItem = null;
            RenderSelectedConnectionDetails();
            return;
        }

        foreach (var item in ConnectionsList.Items.OfType<ListViewItem>())
        {
            if (string.Equals(item.Tag as string, endpointId, StringComparison.Ordinal))
            {
                ConnectionsList.SelectedItem = item;
                break;
            }
        }

        RenderSelectedConnectionDetails();
    }

    private void RenderSelectedConnectionDetails()
    {
        var endpoint = GetSelectedEndpoint();

        _isUpdatingConnectionDetails = true;

        try
        {
            var hasSelection = endpoint is not null;

            SelectedConnectionHintText.Text = hasSelection
                ? "Edit the selected connection configuration. Apply Configuration to persist changes."
                : "Select a connection to edit its settings.";
            SelectedConnectionIdTextBox.Text = endpoint?.Id ?? string.Empty;
            SelectedConnectionIdTextBox.IsEnabled = hasSelection;
            DraftDisplayNameTextBox.Text = endpoint?.DisplayName ?? string.Empty;
            DraftDisplayNameTextBox.IsEnabled = hasSelection;
            DraftEndpointUrlTextBox.Text = endpoint?.EndpointUrl ?? string.Empty;
            DraftEndpointUrlTextBox.IsEnabled = hasSelection;
            DraftEnabledToggle.IsOn = endpoint?.Enabled ?? false;
            DraftEnabledToggle.IsEnabled = hasSelection;
        }
        finally
        {
            _isUpdatingConnectionDetails = false;
        }
    }

    private UpstreamEndpointConfiguration? GetSelectedEndpoint()
    {
        return _draftEndpoints.Endpoints.FirstOrDefault(endpoint => endpoint.Id == _selectedEndpointId);
    }

    private void UpdateSelectedEndpoint(Func<UpstreamEndpointConfiguration, UpstreamEndpointConfiguration> update)
    {
        var index = _draftEndpoints.Endpoints.FindIndex(endpoint => endpoint.Id == _selectedEndpointId);
        if (index < 0)
        {
            return;
        }

        _draftEndpoints.Endpoints[index] = update(_draftEndpoints.Endpoints[index]);
        ConnectionsApplyStatusText.Text = "Configuration updated. Apply Configuration to persist changes.";
        RenderConnectionsDraft();
    }

    private bool HasUnsavedChanges()
    {
        return !string.Equals(_lastLoadedConfigurationHash, ComputeConfigurationHash(_draftEndpoints), StringComparison.Ordinal);
    }

    private static string ComputeConfigurationHash(UpstreamEndpointConfigurationDocument document)
    {
        var json = JsonSerializer.Serialize(document);
        return json;
    }

    private void SetOperationInProgress(bool value)
    {
        _operationInProgress = value;
        ApplyConfigurationButton.IsEnabled = !value;
        ReloadConfigurationButton.IsEnabled = !value;
    }
}