using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Text.Json;
using UAGateway.Core.Configuration;
using UAGateway.UI.Services;

namespace UAGateway.UI.Controls;

public sealed partial class ConnectionsEditor : UserControl
{
    private const string DefaultSecurityMode = "SignAndEncrypt";
    private const string DefaultSecurityPolicy = "Basic256Sha256";
    private const string DefaultAuthenticationMode = "Anonymous";
    private const string DefaultRetryStrategy = "Exponential";
    private const string DefaultEndpointUrl = "opc.tcp://localhost:4840";

    private UpstreamEndpointConfigurationDocument _draftEndpoints = new();
    private string _lastLoadedConfigurationHash = string.Empty;
    private string? _selectedEndpointId;
    private bool _isUpdatingConnectionDetails;
    private bool _operationInProgress;
    private readonly IpcControlClient _ipcControlClient = new();

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
            Content = "Get Active Config will replace current unsaved edits with the active configuration used by the service.",
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
        ConnectionsApplyStatusText.Text = "Active configuration loaded from config store.";

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
            EndpointUrl = DefaultEndpointUrl,
            Enabled = true,
            Authentication = new UpstreamEndpointAuthenticationSettings
            {
                Mode = DefaultAuthenticationMode,
                CredentialId = string.Empty,
            },
            Security = new UpstreamEndpointSecuritySettings
            {
                SecurityMode = DefaultSecurityMode,
                SecurityPolicy = DefaultSecurityPolicy,
                AutoAcceptUntrustedCertificates = false,
            },
            Transport = new UpstreamEndpointTransportSettings
            {
                ConnectionTimeoutMs = 5000,
                OperationTimeoutMs = 15000,
                SessionTimeoutMs = 60000,
            },
            Subscription = new UpstreamEndpointSubscriptionSettings
            {
                PublishingIntervalMs = 1000,
                SamplingIntervalMs = 1000,
                QueueSize = 100,
                MaxItemsPerSubscription = 500,
                KeepAliveCount = 10,
                LifetimeCount = 30,
                MaxNotificationsPerPublish = 0,
                PublishingEnabled = true,
                Priority = 0,
                DiscardOldest = true,
            },
            Retry = new UpstreamEndpointRetrySettings
            {
                Strategy = DefaultRetryStrategy,
                InitialDelaySeconds = 2,
                MaxDelaySeconds = 60,
                SuccessProbeIntervalSeconds = 30,
                MaxAttempts = 0,
                ReconnectOnFailure = true,
            },
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

    private async void ApplyConnectionsDraft_Click(object sender, RoutedEventArgs e)
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

        ConnectionsApplyStatusText.Text = "Configuration saved. Applying to running service...";
        var applyResponse = await _ipcControlClient.TryApplyDraftConfigurationAsync(_draftEndpoints);

        if (applyResponse is null)
        {
            ConnectionsApplyStatusText.Text =
                "Apply partially succeeded. Configuration saved, but service notification was unavailable.";
            RenderConnectionsDraft();
            SetOperationInProgress(false);
            return;
        }

        if (!applyResponse.Applied)
        {
            var firstIssue = applyResponse.Issues.FirstOrDefault();
            ConnectionsApplyStatusText.Text = firstIssue is null
                ? "Apply failed in service validation."
                : $"Apply failed in service validation: {firstIssue.Target} - {firstIssue.Message}";
            RenderConnectionsDraft();
            SetOperationInProgress(false);
            return;
        }

        ConnectionsApplyStatusText.Text =
            $"Apply succeeded. Configuration saved and applied to running service (correlation {applyResponse.CorrelationId}).";
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

    private void DraftEndpointDiscoveryButton_Click(object sender, RoutedEventArgs e)
    {
        ConnectionsApplyStatusText.Text = "Endpoint discovery will be added in a future update.";
    }

    private void DraftTestConnectionButton_Click(object sender, RoutedEventArgs e)
    {
        throw new NotImplementedException("Test connection is not implemented yet.");
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

    private void DraftSecurityModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingConnectionDetails)
        {
            return;
        }

        var selected = GetSelectedComboBoxValue(DraftSecurityModeComboBox, DefaultSecurityMode);
        UpdateSelectedEndpoint(endpoint => endpoint with
        {
            Security = EnsureSecurity(endpoint.Security) with
            {
                SecurityMode = selected,
            },
        });
    }

    private void DraftSecurityPolicyComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingConnectionDetails)
        {
            return;
        }

        var selected = GetSelectedComboBoxValue(DraftSecurityPolicyComboBox, DefaultSecurityPolicy);
        UpdateSelectedEndpoint(endpoint => endpoint with
        {
            Security = EnsureSecurity(endpoint.Security) with
            {
                SecurityPolicy = selected,
            },
        });
    }

    private void DraftAutoAcceptUntrustedToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingConnectionDetails)
        {
            return;
        }

        UpdateSelectedEndpoint(endpoint => endpoint with
        {
            Security = EnsureSecurity(endpoint.Security) with
            {
                AutoAcceptUntrustedCertificates = DraftAutoAcceptUntrustedToggle.IsOn,
            },
        });
    }

    private void DraftViewCertificateButton_Click(object sender, RoutedEventArgs e)
    {
        throw new NotImplementedException("View certificate is not implemented yet.");
    }

    private void DraftAuthenticationModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingConnectionDetails)
        {
            return;
        }

        var selected = GetSelectedComboBoxValue(DraftAuthenticationModeComboBox, DefaultAuthenticationMode);
        UpdateSelectedEndpoint(endpoint =>
        {
            var authentication = EnsureAuthentication(endpoint.Authentication) with
            {
                Mode = selected,
            };

            if (string.Equals(selected, "UsernamePassword", StringComparison.OrdinalIgnoreCase)
                && string.IsNullOrWhiteSpace(authentication.CredentialId))
            {
                authentication = authentication with
                {
                    CredentialId = endpoint.Id,
                };
            }

            return endpoint with
            {
                Authentication = authentication,
            };
        });
    }

    private void DraftCredentialUsernameTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingConnectionDetails)
        {
            return;
        }

        SaveCredentialForSelectedEndpoint();
    }

    private void DraftCredentialPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingConnectionDetails)
        {
            return;
        }

        SaveCredentialForSelectedEndpoint();
    }

    private void DraftConnectionTimeoutTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingConnectionDetails)
        {
            return;
        }

        var fallback = GetSelectedEndpoint()?.Transport.ConnectionTimeoutMs ?? 5000;
        var value = ParseIntWithFallback(DraftConnectionTimeoutTextBox.Text, fallback);
        UpdateSelectedEndpoint(endpoint => endpoint with
        {
            Transport = EnsureTransport(endpoint.Transport) with
            {
                ConnectionTimeoutMs = value,
            },
        });
    }

    private void DraftOperationTimeoutTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingConnectionDetails)
        {
            return;
        }

        var fallback = GetSelectedEndpoint()?.Transport.OperationTimeoutMs ?? 15000;
        var value = ParseIntWithFallback(DraftOperationTimeoutTextBox.Text, fallback);
        UpdateSelectedEndpoint(endpoint => endpoint with
        {
            Transport = EnsureTransport(endpoint.Transport) with
            {
                OperationTimeoutMs = value,
            },
        });
    }

    private void DraftSessionTimeoutTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingConnectionDetails)
        {
            return;
        }

        var fallback = GetSelectedEndpoint()?.Transport.SessionTimeoutMs ?? 60000;
        var value = ParseIntWithFallback(DraftSessionTimeoutTextBox.Text, fallback);
        UpdateSelectedEndpoint(endpoint => endpoint with
        {
            Transport = EnsureTransport(endpoint.Transport) with
            {
                SessionTimeoutMs = value,
            },
        });
    }

    private void DraftPublishingIntervalTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingConnectionDetails)
        {
            return;
        }

        var fallback = GetSelectedEndpoint()?.Subscription.PublishingIntervalMs ?? 1000;
        var value = ParseIntWithFallback(DraftPublishingIntervalTextBox.Text, fallback);
        UpdateSelectedEndpoint(endpoint => endpoint with
        {
            Subscription = EnsureSubscription(endpoint.Subscription) with
            {
                PublishingIntervalMs = value,
            },
        });
    }

    private void DraftSamplingIntervalTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingConnectionDetails)
        {
            return;
        }

        var fallback = GetSelectedEndpoint()?.Subscription.SamplingIntervalMs ?? 1000;
        var value = ParseIntWithFallback(DraftSamplingIntervalTextBox.Text, fallback);
        UpdateSelectedEndpoint(endpoint => endpoint with
        {
            Subscription = EnsureSubscription(endpoint.Subscription) with
            {
                SamplingIntervalMs = value,
            },
        });
    }

    private void DraftQueueSizeTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingConnectionDetails)
        {
            return;
        }

        var fallback = GetSelectedEndpoint()?.Subscription.QueueSize ?? 100;
        var value = ParseIntWithFallback(DraftQueueSizeTextBox.Text, fallback);
        UpdateSelectedEndpoint(endpoint => endpoint with
        {
            Subscription = EnsureSubscription(endpoint.Subscription) with
            {
                QueueSize = value,
            },
        });
    }

    private void DraftMaxItemsPerSubscriptionTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingConnectionDetails)
        {
            return;
        }

        var fallback = GetSelectedEndpoint()?.Subscription.MaxItemsPerSubscription ?? 500;
        var value = ParseIntWithFallback(DraftMaxItemsPerSubscriptionTextBox.Text, fallback);
        UpdateSelectedEndpoint(endpoint => endpoint with
        {
            Subscription = EnsureSubscription(endpoint.Subscription) with
            {
                MaxItemsPerSubscription = value,
            },
        });
    }

    private void DraftKeepAliveCountTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingConnectionDetails)
        {
            return;
        }

        var fallback = GetSelectedEndpoint()?.Subscription.KeepAliveCount ?? 10;
        var value = ParseIntWithFallback(DraftKeepAliveCountTextBox.Text, fallback);
        UpdateSelectedEndpoint(endpoint => endpoint with
        {
            Subscription = EnsureSubscription(endpoint.Subscription) with
            {
                KeepAliveCount = value,
            },
        });
    }

    private void DraftLifetimeCountTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingConnectionDetails)
        {
            return;
        }

        var fallback = GetSelectedEndpoint()?.Subscription.LifetimeCount ?? 30;
        var value = ParseIntWithFallback(DraftLifetimeCountTextBox.Text, fallback);
        UpdateSelectedEndpoint(endpoint => endpoint with
        {
            Subscription = EnsureSubscription(endpoint.Subscription) with
            {
                LifetimeCount = value,
            },
        });
    }

    private void DraftMaxNotificationsPerPublishTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingConnectionDetails)
        {
            return;
        }

        var fallback = GetSelectedEndpoint()?.Subscription.MaxNotificationsPerPublish ?? 0;
        var value = ParseIntWithFallback(DraftMaxNotificationsPerPublishTextBox.Text, fallback);
        UpdateSelectedEndpoint(endpoint => endpoint with
        {
            Subscription = EnsureSubscription(endpoint.Subscription) with
            {
                MaxNotificationsPerPublish = value,
            },
        });
    }

    private void DraftSubscriptionPriorityTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingConnectionDetails)
        {
            return;
        }

        var fallback = GetSelectedEndpoint()?.Subscription.Priority ?? 0;
        var value = ParseIntWithFallback(DraftSubscriptionPriorityTextBox.Text, fallback);
        UpdateSelectedEndpoint(endpoint => endpoint with
        {
            Subscription = EnsureSubscription(endpoint.Subscription) with
            {
                Priority = value,
            },
        });
    }

    private void DraftPublishingEnabledToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingConnectionDetails)
        {
            return;
        }

        UpdateSelectedEndpoint(endpoint => endpoint with
        {
            Subscription = EnsureSubscription(endpoint.Subscription) with
            {
                PublishingEnabled = DraftPublishingEnabledToggle.IsOn,
            },
        });
    }

    private void DraftDiscardOldestToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingConnectionDetails)
        {
            return;
        }

        UpdateSelectedEndpoint(endpoint => endpoint with
        {
            Subscription = EnsureSubscription(endpoint.Subscription) with
            {
                DiscardOldest = DraftDiscardOldestToggle.IsOn,
            },
        });
    }

    private void DraftRetryStrategyComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingConnectionDetails)
        {
            return;
        }

        var selected = GetSelectedComboBoxValue(DraftRetryStrategyComboBox, DefaultRetryStrategy);
        UpdateSelectedEndpoint(endpoint => endpoint with
        {
            Retry = EnsureRetry(endpoint.Retry) with
            {
                Strategy = selected,
            },
        });
    }

    private void DraftRetryInitialDelayTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingConnectionDetails)
        {
            return;
        }

        var fallback = GetSelectedEndpoint()?.Retry.InitialDelaySeconds ?? 2;
        var value = ParseIntWithFallback(DraftRetryInitialDelayTextBox.Text, fallback);
        UpdateSelectedEndpoint(endpoint => endpoint with
        {
            Retry = EnsureRetry(endpoint.Retry) with
            {
                InitialDelaySeconds = value,
            },
        });
    }

    private void DraftRetryMaxDelayTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingConnectionDetails)
        {
            return;
        }

        var fallback = GetSelectedEndpoint()?.Retry.MaxDelaySeconds ?? 60;
        var value = ParseIntWithFallback(DraftRetryMaxDelayTextBox.Text, fallback);
        UpdateSelectedEndpoint(endpoint => endpoint with
        {
            Retry = EnsureRetry(endpoint.Retry) with
            {
                MaxDelaySeconds = value,
            },
        });
    }

    private void DraftRetryMaxAttemptsTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingConnectionDetails)
        {
            return;
        }

        var fallback = GetSelectedEndpoint()?.Retry.MaxAttempts ?? 0;
        var value = ParseIntWithFallback(DraftRetryMaxAttemptsTextBox.Text, fallback);
        UpdateSelectedEndpoint(endpoint => endpoint with
        {
            Retry = EnsureRetry(endpoint.Retry) with
            {
                MaxAttempts = value,
            },
        });
    }

    private void DraftSuccessProbeIntervalTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingConnectionDetails)
        {
            return;
        }

        var fallback = GetSelectedEndpoint()?.Retry.SuccessProbeIntervalSeconds ?? 30;
        var value = ParseIntWithFallback(DraftSuccessProbeIntervalTextBox.Text, fallback);
        UpdateSelectedEndpoint(endpoint => endpoint with
        {
            Retry = EnsureRetry(endpoint.Retry) with
            {
                SuccessProbeIntervalSeconds = value,
            },
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
            DraftDisplayNameTextBox.Text = endpoint?.DisplayName ?? string.Empty;
            DraftDisplayNameTextBox.IsEnabled = hasSelection;
            DraftEndpointUrlTextBox.Text = hasSelection
                ? (string.IsNullOrWhiteSpace(endpoint?.EndpointUrl) ? DefaultEndpointUrl : endpoint.EndpointUrl)
                : DefaultEndpointUrl;
            DraftEndpointUrlTextBox.IsEnabled = hasSelection;
            DraftTestConnectionButton.IsEnabled = hasSelection;

            var security = EnsureSecurity(endpoint?.Security);
            SelectComboBoxItemByContent(DraftSecurityModeComboBox, security.SecurityMode);
            SelectComboBoxItemByContent(DraftSecurityPolicyComboBox, security.SecurityPolicy);
            DraftAutoAcceptUntrustedToggle.IsOn = security.AutoAcceptUntrustedCertificates;
            DraftSecurityModeComboBox.IsEnabled = hasSelection;
            DraftSecurityPolicyComboBox.IsEnabled = hasSelection;
            DraftAutoAcceptUntrustedToggle.IsEnabled = hasSelection;
            DraftViewCertificateButton.IsEnabled = hasSelection;

            var authentication = EnsureAuthentication(endpoint?.Authentication);
            SelectComboBoxItemByContent(DraftAuthenticationModeComboBox, authentication.Mode);
            DraftAuthenticationModeComboBox.IsEnabled = hasSelection;

            var transport = EnsureTransport(endpoint?.Transport);
            DraftConnectionTimeoutTextBox.Text = transport.ConnectionTimeoutMs.ToString();
            DraftOperationTimeoutTextBox.Text = transport.OperationTimeoutMs.ToString();
            DraftSessionTimeoutTextBox.Text = transport.SessionTimeoutMs.ToString();
            DraftConnectionTimeoutTextBox.IsEnabled = hasSelection;
            DraftOperationTimeoutTextBox.IsEnabled = hasSelection;
            DraftSessionTimeoutTextBox.IsEnabled = hasSelection;

            var subscription = EnsureSubscription(endpoint?.Subscription);
            DraftPublishingIntervalTextBox.Text = subscription.PublishingIntervalMs.ToString();
            DraftSamplingIntervalTextBox.Text = subscription.SamplingIntervalMs.ToString();
            DraftQueueSizeTextBox.Text = subscription.QueueSize.ToString();
            DraftMaxItemsPerSubscriptionTextBox.Text = subscription.MaxItemsPerSubscription.ToString();
            DraftKeepAliveCountTextBox.Text = subscription.KeepAliveCount.ToString();
            DraftLifetimeCountTextBox.Text = subscription.LifetimeCount.ToString();
            DraftMaxNotificationsPerPublishTextBox.Text = subscription.MaxNotificationsPerPublish.ToString();
            DraftSubscriptionPriorityTextBox.Text = subscription.Priority.ToString();
            DraftPublishingEnabledToggle.IsOn = subscription.PublishingEnabled;
            DraftDiscardOldestToggle.IsOn = subscription.DiscardOldest;
            DraftPublishingIntervalTextBox.IsEnabled = hasSelection;
            DraftSamplingIntervalTextBox.IsEnabled = hasSelection;
            DraftQueueSizeTextBox.IsEnabled = hasSelection;
            DraftMaxItemsPerSubscriptionTextBox.IsEnabled = hasSelection;
            DraftKeepAliveCountTextBox.IsEnabled = hasSelection;
            DraftLifetimeCountTextBox.IsEnabled = hasSelection;
            DraftMaxNotificationsPerPublishTextBox.IsEnabled = hasSelection;
            DraftSubscriptionPriorityTextBox.IsEnabled = hasSelection;
            DraftPublishingEnabledToggle.IsEnabled = hasSelection;
            DraftDiscardOldestToggle.IsEnabled = hasSelection;

            var retry = EnsureRetry(endpoint?.Retry);
            SelectComboBoxItemByContent(DraftRetryStrategyComboBox, retry.Strategy);
            DraftRetryInitialDelayTextBox.Text = retry.InitialDelaySeconds.ToString();
            DraftRetryMaxDelayTextBox.Text = retry.MaxDelaySeconds.ToString();
            DraftRetryMaxAttemptsTextBox.Text = retry.MaxAttempts.ToString();
            DraftSuccessProbeIntervalTextBox.Text = retry.SuccessProbeIntervalSeconds.ToString();
            DraftRetryStrategyComboBox.IsEnabled = hasSelection;
            DraftRetryInitialDelayTextBox.IsEnabled = hasSelection;
            DraftRetryMaxDelayTextBox.IsEnabled = hasSelection;
            DraftRetryMaxAttemptsTextBox.IsEnabled = hasSelection;
            DraftSuccessProbeIntervalTextBox.IsEnabled = hasSelection;

            var credentialId = string.IsNullOrWhiteSpace(authentication.CredentialId)
                ? endpoint?.Id ?? string.Empty
                : authentication.CredentialId;
            var credential = UpstreamEndpointCredentialStore.TryLoadUsernamePassword(credentialId);
            DraftCredentialUsernameTextBox.Text = credential?.Username ?? string.Empty;
            DraftCredentialPasswordBox.Password = credential?.Password ?? string.Empty;

            var usesCredentials = hasSelection && string.Equals(authentication.Mode, "UsernamePassword", StringComparison.OrdinalIgnoreCase);
            DraftCredentialUsernameTextBox.IsEnabled = usesCredentials;
            DraftCredentialPasswordBox.IsEnabled = usesCredentials;
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

    private void SaveCredentialForSelectedEndpoint()
    {
        var endpoint = GetSelectedEndpoint();
        if (endpoint is null)
        {
            return;
        }

        if (!string.Equals(endpoint.Authentication.Mode, "UsernamePassword", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var credentialId = endpoint.Authentication.CredentialId;
        if (string.IsNullOrWhiteSpace(credentialId))
        {
            credentialId = endpoint.Id;

            var localCredentialId = credentialId;
            UpdateSelectedEndpoint(current => current with
            {
                Authentication = EnsureAuthentication(current.Authentication) with
                {
                    CredentialId = localCredentialId,
                },
            });
        }

        var username = (DraftCredentialUsernameTextBox.Text ?? string.Empty).Trim();
        var password = DraftCredentialPasswordBox.Password ?? string.Empty;

        if (string.IsNullOrWhiteSpace(username) && string.IsNullOrWhiteSpace(password))
        {
            UpstreamEndpointCredentialStore.RemoveCredential(credentialId);
            ConnectionsApplyStatusText.Text = "Credentials cleared for selected endpoint.";
            return;
        }

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            ConnectionsApplyStatusText.Text = "Username and password are both required for UsernamePassword mode.";
            return;
        }

        try
        {
            UpstreamEndpointCredentialStore.SaveUsernamePassword(credentialId, username, password);
            ConnectionsApplyStatusText.Text = "Credential updated. Apply Configuration to persist endpoint settings.";
        }
        catch (Exception ex)
        {
            ConnectionsApplyStatusText.Text = $"Credential save failed: {ex.Message}";
        }
    }

    private static int ParseIntWithFallback(string? value, int fallback)
    {
        if (int.TryParse(value, out var parsed))
        {
            return parsed;
        }

        return fallback;
    }

    private static void SelectComboBoxItemByContent(ComboBox comboBox, string value)
    {
        foreach (var item in comboBox.Items.OfType<ComboBoxItem>())
        {
            if (string.Equals(item.Content?.ToString(), value, StringComparison.OrdinalIgnoreCase))
            {
                comboBox.SelectedItem = item;
                return;
            }
        }

        comboBox.SelectedIndex = comboBox.Items.Count > 0 ? 0 : -1;
    }

    private static string GetSelectedComboBoxValue(ComboBox comboBox, string fallback)
    {
        if (comboBox.SelectedItem is ComboBoxItem item && item.Content is string content)
        {
            return content;
        }

        return fallback;
    }

    private static UpstreamEndpointSecuritySettings EnsureSecurity(UpstreamEndpointSecuritySettings? security)
    {
        return security ?? new UpstreamEndpointSecuritySettings
        {
            SecurityMode = DefaultSecurityMode,
            SecurityPolicy = DefaultSecurityPolicy,
            AutoAcceptUntrustedCertificates = false,
        };
    }

    private static UpstreamEndpointAuthenticationSettings EnsureAuthentication(UpstreamEndpointAuthenticationSettings? authentication)
    {
        return authentication ?? new UpstreamEndpointAuthenticationSettings
        {
            Mode = DefaultAuthenticationMode,
            CredentialId = string.Empty,
        };
    }

    private static UpstreamEndpointTransportSettings EnsureTransport(UpstreamEndpointTransportSettings? transport)
    {
        return transport ?? new UpstreamEndpointTransportSettings
        {
            ConnectionTimeoutMs = 5000,
            OperationTimeoutMs = 15000,
            SessionTimeoutMs = 60000,
        };
    }

    private static UpstreamEndpointRetrySettings EnsureRetry(UpstreamEndpointRetrySettings? retry)
    {
        return retry ?? new UpstreamEndpointRetrySettings
        {
            Strategy = DefaultRetryStrategy,
            InitialDelaySeconds = 2,
            MaxDelaySeconds = 60,
            SuccessProbeIntervalSeconds = 30,
            MaxAttempts = 0,
            ReconnectOnFailure = true,
        };
    }

    private static UpstreamEndpointSubscriptionSettings EnsureSubscription(UpstreamEndpointSubscriptionSettings? subscription)
    {
        return subscription ?? new UpstreamEndpointSubscriptionSettings
        {
            PublishingIntervalMs = 1000,
            SamplingIntervalMs = 1000,
            QueueSize = 100,
            MaxItemsPerSubscription = 500,
            KeepAliveCount = 10,
            LifetimeCount = 30,
            MaxNotificationsPerPublish = 0,
            PublishingEnabled = true,
            Priority = 0,
            DiscardOldest = true,
        };
    }
}