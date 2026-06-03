using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using UAGateway.Core.Configuration;

namespace UAGateway.UI.Controls;

public sealed partial class ConnectionsEditor : UserControl
{
    private UpstreamEndpointConfigurationDocument _draftEndpoints = new();
    private string? _selectedEndpointId;
    private bool _isUpdatingConnectionDetails;

    public ConnectionsEditor()
    {
        InitializeComponent();
    }

    public void ReloadDraft()
    {
        _draftEndpoints = UpstreamEndpointConfigurationStore.LoadOrCreateDefault();
        ConnectionsApplyStatusText.Text = "Draft reloaded from disk.";

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
        ConnectionsHeaderText.Text = $"Draft endpoints: {_draftEndpoints.Endpoints.Count}";

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
        ConnectionsApplyStatusText.Text = "Endpoint added to draft. Apply Draft to persist.";
    }

    private void ReloadConnectionsDraft_Click(object sender, RoutedEventArgs e)
    {
        ReloadDraft();
    }

    private void ApplyConnectionsDraft_Click(object sender, RoutedEventArgs e)
    {
        var issues = UpstreamEndpointConfigurationValidator.Validate(_draftEndpoints);
        if (issues.Count > 0)
        {
            var firstIssue = issues[0];
            ConnectionsApplyStatusText.Text =
                $"Apply failed. Validation issues: {issues.Count}. First issue: [{firstIssue.EndpointId}] {firstIssue.Message}";
            return;
        }

        UpstreamEndpointConfigurationStore.Save(_draftEndpoints);
        ConnectionsApplyStatusText.Text = "Apply succeeded. Draft saved to upstream endpoint configuration file.";
        RenderConnectionsDraft();
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
                ? "Edit the selected draft connection. Apply Draft to persist changes."
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
        ConnectionsApplyStatusText.Text = "Draft updated. Apply Draft to persist changes.";
        RenderConnectionsDraft();
    }
}