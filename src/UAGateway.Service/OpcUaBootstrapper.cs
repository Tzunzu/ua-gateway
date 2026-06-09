using Microsoft.Extensions.Logging;
using Opc.Ua;
using Opc.Ua.Configuration;
using Opc.Ua.Server;
using UAGateway.Core.Configuration;
using UAGateway.Core.Diagnostics;
using UAGateway.Core.Health;

namespace UAGateway.Service;

internal sealed class OpcUaBootstrapper
{
    private readonly ILogger<OpcUaBootstrapper> _logger;
    private readonly StartupHealthState _startupHealthState;
    private readonly UpstreamConnectionLifecycleManager _connectionLifecycleManager;
    private ApplicationInstance? _localServerApplicationInstance;
    private UAGatewayLocalServer? _localServer;

    public OpcUaBootstrapper(
        ILogger<OpcUaBootstrapper> logger,
        StartupHealthState startupHealthState,
        UpstreamConnectionLifecycleManager connectionLifecycleManager)
    {
        _logger = logger;
        _startupHealthState = startupHealthState;
        _connectionLifecycleManager = connectionLifecycleManager;
    }

    public void Initialize()
    {
        var configApplyCorrelationId = CreateCorrelationId();
        PublishStartupHealth(
            StartupHealthStatus.Degraded,
            "Startup configuration flow in progress.",
            configApplyCorrelationId);

        try
        {
            using (_logger.BeginScope("CorrelationId:{CorrelationId}", configApplyCorrelationId))
            {
                GatewayLogMessages.ConfigApplyStarted(_logger, configApplyCorrelationId);
                GatewayLogMessages.OpcUaConfigurationBuildStarted(_logger, configApplyCorrelationId);

                var localServerConfiguration = LoadValidatedLocalServerConfiguration();
                var applicationConfiguration = BuildApplicationConfiguration(localServerConfiguration);
                var upstreamEndpointConfiguration = LoadValidatedUpstreamEndpointConfiguration();
                var mappingConfiguration = LoadValidatedMappingConfiguration(upstreamEndpointConfiguration);
                var enabledEndpointCount = upstreamEndpointConfiguration.Endpoints.Count(endpoint => endpoint.Enabled);
                _connectionLifecycleManager.SetApplicationConfiguration(applicationConfiguration);
                _connectionLifecycleManager.ApplyConfiguration(upstreamEndpointConfiguration);

                ValidateApplicationConfiguration(applicationConfiguration);
                GatewayLogMessages.OpcUaConfigurationValidated(
                    _logger,
                    applicationConfiguration.ApplicationUri ?? "unknown");

                var securitySnapshot = InitializeCertificateStores(applicationConfiguration);
                StartLocalServerEndpoint(applicationConfiguration, mappingConfiguration, configApplyCorrelationId);

                // This confirms the OPC UA stack package is available and wired into the service.
                var statusCode = StatusCodes.Good;
                GatewayLogMessages.OpcUaBootstrapInitialized(_logger, statusCode);

                var startupStatus = StartupHealthStatus.Healthy;
                var startupReason = "Configuration and security bootstrap completed.";

                if (securitySnapshot.Status == SecurityBootstrapStatus.Degraded)
                {
                    startupStatus = StartupHealthStatus.Degraded;
                    startupReason = "Startup completed with security trust warnings.";
                }

                if (enabledEndpointCount == 0)
                {
                    startupStatus = StartupHealthStatus.Degraded;
                    startupReason = securitySnapshot.Status == SecurityBootstrapStatus.Degraded
                        ? "Startup completed with security trust warnings and without configured upstream endpoints."
                        : "Startup completed without configured upstream endpoints.";
                }

                PublishStartupHealth(startupStatus, startupReason, configApplyCorrelationId);

                GatewayLogMessages.ConfigApplyCompleted(_logger, configApplyCorrelationId);
            }

            var reconnectCorrelationId = CreateCorrelationId();

            using (_logger.BeginScope("CorrelationId:{CorrelationId}", reconnectCorrelationId))
            {
                GatewayLogMessages.ReconnectFlowStarted(_logger, reconnectCorrelationId);
                GatewayLogMessages.ConnectionManagerInitialized(_logger);

                var enabledEndpointCount = _connectionLifecycleManager.EnabledEndpointCount;

                if (enabledEndpointCount == 0)
                {
                    GatewayLogMessages.NoUpstreamEndpointsConfigured(_logger, reconnectCorrelationId);
                    GatewayLogMessages.ReconnectFlowIdleNoEndpoints(_logger, reconnectCorrelationId);

                    PublishStartupHealth(
                        StartupHealthStatus.Degraded,
                        "Startup completed without configured upstream endpoints.",
                        reconnectCorrelationId);
                }
                else
                {
                    GatewayLogMessages.UpstreamEndpointsConfigured(_logger, enabledEndpointCount);
                }
            }
        }
        catch (Exception ex)
        {
            var faultCorrelationId = CreateCorrelationId();
            var startupFailureReason = BuildStartupFailureReason(ex);
            PublishStartupHealth(
                StartupHealthStatus.Faulted,
                startupFailureReason,
                faultCorrelationId);

            PublishSecurityDiagnosticsSnapshot(new SecurityBootstrapDiagnosticsSnapshot(
                SecurityBootstrapStatus.Faulted,
                startupFailureReason,
                DateTimeOffset.UtcNow,
                faultCorrelationId,
                null,
                0,
                0,
                0,
                false,
                true,
                2048));

            throw;
        }
    }

    internal static string BuildStartupFailureReason(Exception ex)
    {
        var message = ex.Message;

        // Local server settings
        if (message.Contains("Local server configuration failed validation", StringComparison.OrdinalIgnoreCase))
        {
            return "Startup failed: Local server settings are invalid. Fix config/server-settings.json and restart.";
        }

        if (message.Contains("Local server configuration file contains invalid JSON", StringComparison.OrdinalIgnoreCase))
        {
            return "Startup failed: Local server settings JSON is malformed. Fix config/server-settings.json and restart.";
        }

        if (message.Contains("Local server configuration file is empty or invalid", StringComparison.OrdinalIgnoreCase))
        {
            return "Startup failed: Local server settings file is empty or unreadable. Fix config/server-settings.json and restart.";
        }

        // Upstream endpoint configuration
        if (message.Contains("Upstream endpoint configuration failed validation", StringComparison.OrdinalIgnoreCase))
        {
            return "Startup failed: Upstream endpoint configuration is invalid. Fix config/upstream-endpoints.json and restart.";
        }

        if (message.Contains("Upstream endpoint configuration file contains invalid JSON", StringComparison.OrdinalIgnoreCase))
        {
            return "Startup failed: Upstream endpoint configuration JSON is malformed. Fix config/upstream-endpoints.json and restart.";
        }

        if (message.Contains("Upstream endpoint configuration file is empty or invalid", StringComparison.OrdinalIgnoreCase))
        {
            return "Startup failed: Upstream endpoint configuration file is empty or unreadable. Fix config/upstream-endpoints.json and restart.";
        }

        // Namespace mapping configuration
        if (message.Contains("Namespace mapping configuration failed validation", StringComparison.OrdinalIgnoreCase))
        {
            return "Startup failed: Namespace mapping configuration is invalid. Fix config/namespace-mapping.json and restart.";
        }

        if (message.Contains("Namespace mapping configuration file contains invalid JSON", StringComparison.OrdinalIgnoreCase))
        {
            return "Startup failed: Namespace mapping configuration JSON is malformed. Fix config/namespace-mapping.json and restart.";
        }

        if (message.Contains("Namespace mapping configuration file is empty or invalid", StringComparison.OrdinalIgnoreCase))
        {
            return "Startup failed: Namespace mapping configuration file is empty or unreadable. Fix config/namespace-mapping.json and restart.";
        }

        // Certificate bootstrap and trust workflow
        if (message.Contains("OPC UA certificate bootstrap failed at startup", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("Application certificate is missing or invalid", StringComparison.OrdinalIgnoreCase))
        {
            return "Startup failed: Certificate bootstrap failed. Verify application certificate and trust stores under %ProgramData%\\UA Gateway\\pki, then restart.";
        }

        return $"Startup failed: {message}";
    }

    private static string CreateCorrelationId()
    {
        return Guid.NewGuid().ToString("N");
    }

    private static ApplicationConfiguration BuildApplicationConfiguration(LocalServerConfigurationDocument localServerConfiguration)
    {
        var hostName = Utils.GetHostName();
        var appDataRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "UA Gateway");
        var pkiRoot = Path.Combine(appDataRoot, "pki");
        var localServerBaseAddress = localServerConfiguration.BuildBaseAddress();
        var applicationName = string.IsNullOrWhiteSpace(localServerConfiguration.ApplicationName)
            ? "UA Gateway"
            : localServerConfiguration.ApplicationName.Trim();
        var applicationNameSlug = applicationName.Replace(' ', '_');

        var ownStorePath = Path.Combine(pkiRoot, "own");
        var trustedStorePath = Path.Combine(pkiRoot, "trusted");
        var issuerStorePath = Path.Combine(pkiRoot, "issuer");
        var rejectedStorePath = Path.Combine(pkiRoot, "rejected");

        Directory.CreateDirectory(ownStorePath);
        Directory.CreateDirectory(trustedStorePath);
        Directory.CreateDirectory(issuerStorePath);
        Directory.CreateDirectory(rejectedStorePath);

        var userTokenPolicies = BuildUserTokenPolicies(localServerConfiguration);

        return new ApplicationConfiguration
        {
            ApplicationName = applicationName,
            ApplicationUri = $"urn:{hostName}:{applicationNameSlug}:Service",
            ProductUri = string.IsNullOrWhiteSpace(localServerConfiguration.ProductUri)
                ? "urn:uagateway:service"
                : localServerConfiguration.ProductUri.Trim(),
            ApplicationType = ApplicationType.ClientAndServer,
            SecurityConfiguration = new SecurityConfiguration
            {
                ApplicationCertificate = new CertificateIdentifier
                {
                    StoreType = CertificateStoreType.Directory,
                    StorePath = ownStorePath,
                    SubjectName = $"CN={applicationName}, DC={hostName}"
                },
                TrustedPeerCertificates = new CertificateTrustList
                {
                    StoreType = CertificateStoreType.Directory,
                    StorePath = trustedStorePath
                },
                TrustedIssuerCertificates = new CertificateTrustList
                {
                    StoreType = CertificateStoreType.Directory,
                    StorePath = issuerStorePath
                },
                RejectedCertificateStore = new CertificateTrustList
                {
                    StoreType = CertificateStoreType.Directory,
                    StorePath = rejectedStorePath
                },
                AutoAcceptUntrustedCertificates = false,
                RejectSHA1SignedCertificates = true,
                MinimumCertificateKeySize = 2048,
                AddAppCertToTrustedStore = false,
            },
            TransportQuotas = new TransportQuotas
            {
                OperationTimeout = 15000,
                MaxStringLength = 1048576,
                MaxByteStringLength = 1048576,
                MaxArrayLength = 65535,
                MaxMessageSize = 4194304,
                MaxBufferSize = 65535,
                ChannelLifetime = 300000,
                SecurityTokenLifetime = 3600000,
            },
            ClientConfiguration = new ClientConfiguration
            {
                DefaultSessionTimeout = 60000,
            },
            ServerConfiguration = new ServerConfiguration
            {
                BaseAddresses = new StringCollection
                {
                    localServerBaseAddress,
                },
                SecurityPolicies = new ServerSecurityPolicyCollection
                {
                    new ServerSecurityPolicy
                    {
                        SecurityMode = MapServerSecurityMode(localServerConfiguration.SecurityMode),
                        SecurityPolicyUri = MapServerSecurityPolicyUri(localServerConfiguration.SecurityPolicy),
                    },
                },
                UserTokenPolicies = userTokenPolicies,
                DiagnosticsEnabled = true,
            },
            DisableHiResClock = false,
        };
    }

    private static UserTokenPolicyCollection BuildUserTokenPolicies(LocalServerConfigurationDocument config)
    {
        var policies = new UserTokenPolicyCollection();

        if (config.AllowAnonymous)
        {
            policies.Add(new UserTokenPolicy(UserTokenType.Anonymous));
        }

        if (config.AllowUsernamePassword)
        {
            policies.Add(new UserTokenPolicy(UserTokenType.UserName));
        }

        if (policies.Count == 0)
        {
            policies.Add(new UserTokenPolicy(UserTokenType.Anonymous));
        }

        return policies;
    }

    private static MessageSecurityMode MapServerSecurityMode(string? mode)
    {
        return mode?.Trim() switch
        {
            "None" => MessageSecurityMode.None,
            "Sign" => MessageSecurityMode.Sign,
            _ => MessageSecurityMode.SignAndEncrypt,
        };
    }

    private static string MapServerSecurityPolicyUri(string? policy)
    {
        return policy?.Trim() switch
        {
            "None" => SecurityPolicies.None,
            "Basic128Rsa15" => SecurityPolicies.Basic128Rsa15,
            "Basic256" => SecurityPolicies.Basic256,
            "Aes128Sha256RsaOaep" => SecurityPolicies.Aes128_Sha256_RsaOaep,
            "Aes256Sha256RsaPss" => SecurityPolicies.Aes256_Sha256_RsaPss,
            _ => SecurityPolicies.Basic256Sha256,
        };
    }

    private void ValidateApplicationConfiguration(ApplicationConfiguration applicationConfiguration)
    {
        try
        {
            applicationConfiguration.ValidateAsync(ApplicationType.ClientAndServer).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            GatewayLogMessages.OpcUaConfigurationValidationFailed(_logger, ex.Message, ex);
            throw new InvalidOperationException(
                "OPC UA application configuration failed validation at startup. Check logs for event ID 4004 and error details.",
                ex);
        }
    }

    private SecurityBootstrapDiagnosticsSnapshot InitializeCertificateStores(ApplicationConfiguration applicationConfiguration)
    {
        var securityCorrelationId = CreateCorrelationId();

        using (_logger.BeginScope("CorrelationId:{CorrelationId}", securityCorrelationId))
        {
            GatewayLogMessages.SecurityBootstrapStarted(_logger, securityCorrelationId);

            try
            {
                var telemetryContext = DefaultTelemetry.Create(_ => { });

                var applicationInstance = new ApplicationInstance
                    (applicationConfiguration, telemetryContext)
                {
                    ApplicationName = applicationConfiguration.ApplicationName,
                    ApplicationType = applicationConfiguration.ApplicationType,
                };

                var hasValidCertificate = applicationInstance
                    .CheckApplicationInstanceCertificatesAsync(
                        false,
                        applicationConfiguration.SecurityConfiguration.MinimumCertificateKeySize,
                        CancellationToken.None)
                    .GetAwaiter()
                    .GetResult();

                if (!hasValidCertificate)
                {
                    throw new InvalidOperationException("Application certificate is missing or invalid.");
                }

                var certificate = applicationConfiguration
                    .SecurityConfiguration
                    .FindApplicationCertificateAsync(
                        applicationConfiguration.ApplicationUri,
                        true,
                        telemetryContext,
                        CancellationToken.None)
                    .GetAwaiter()
                    .GetResult();

                GatewayLogMessages.ApplicationCertificateReady(_logger, certificate?.Thumbprint ?? "unknown");

                var trustedPeerCount = CountCertificatesInDirectoryStore(applicationConfiguration.SecurityConfiguration.TrustedPeerCertificates.StorePath);
                var trustedIssuerCount = CountCertificatesInDirectoryStore(applicationConfiguration.SecurityConfiguration.TrustedIssuerCertificates.StorePath);
                var rejectedCount = CountCertificatesInDirectoryStore(applicationConfiguration.SecurityConfiguration.RejectedCertificateStore.StorePath);

                ApplyTrustPolicyDefaults(applicationConfiguration);

                GatewayLogMessages.TrustStoreStateObserved(_logger, trustedPeerCount, trustedIssuerCount, rejectedCount);

                if (trustedPeerCount == 0)
                {
                    GatewayLogMessages.NoTrustedPeersConfigured(_logger);
                }

                var status = trustedPeerCount == 0
                    ? SecurityBootstrapStatus.Degraded
                    : SecurityBootstrapStatus.Healthy;

                var reason = trustedPeerCount == 0
                    ? "Security bootstrap succeeded, but trust list has no trusted peers yet."
                    : "Security bootstrap succeeded with trusted peers configured.";

                var snapshot = new SecurityBootstrapDiagnosticsSnapshot(
                    status,
                    reason,
                    DateTimeOffset.UtcNow,
                    securityCorrelationId,
                    certificate?.Thumbprint,
                    trustedPeerCount,
                    trustedIssuerCount,
                    rejectedCount,
                    applicationConfiguration.SecurityConfiguration.AutoAcceptUntrustedCertificates,
                    applicationConfiguration.SecurityConfiguration.RejectSHA1SignedCertificates,
                    applicationConfiguration.SecurityConfiguration.MinimumCertificateKeySize);

                PublishSecurityDiagnosticsSnapshot(snapshot);
                return snapshot;
            }
            catch (Exception ex)
            {
                GatewayLogMessages.SecurityBootstrapFailed(_logger, ex.Message, ex);

                var snapshot = new SecurityBootstrapDiagnosticsSnapshot(
                    SecurityBootstrapStatus.Faulted,
                    $"Certificate bootstrap failed: {ex.Message}",
                    DateTimeOffset.UtcNow,
                    securityCorrelationId,
                    null,
                    0,
                    0,
                    0,
                    false,
                    true,
                    2048);

                PublishSecurityDiagnosticsSnapshot(snapshot);

                throw new InvalidOperationException(
                    "OPC UA certificate bootstrap failed at startup. Check logs for event ID 3003 and error details.",
                    ex);
            }
        }
    }

    private static int CountCertificatesInDirectoryStore(string? directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath))
        {
            return 0;
        }

        var derCount = Directory.EnumerateFiles(directoryPath, "*.der", SearchOption.TopDirectoryOnly).Count();
        var cerCount = Directory.EnumerateFiles(directoryPath, "*.cer", SearchOption.TopDirectoryOnly).Count();
        var pemCount = Directory.EnumerateFiles(directoryPath, "*.pem", SearchOption.TopDirectoryOnly).Count();
        var pfxCount = Directory.EnumerateFiles(directoryPath, "*.pfx", SearchOption.TopDirectoryOnly).Count();

        return derCount + cerCount + pemCount + pfxCount;
    }

    private void ApplyTrustPolicyDefaults(ApplicationConfiguration applicationConfiguration)
    {
        var securityConfiguration = applicationConfiguration.SecurityConfiguration;

        securityConfiguration.AutoAcceptUntrustedCertificates = false;
        securityConfiguration.AddAppCertToTrustedStore = false;
        securityConfiguration.RejectSHA1SignedCertificates = true;
        securityConfiguration.MinimumCertificateKeySize = (ushort)Math.Max(2048, (int)securityConfiguration.MinimumCertificateKeySize);

        var telemetryContext = DefaultTelemetry.Create(_ => { });
        applicationConfiguration.CertificateValidator ??= new CertificateValidator(telemetryContext);
        applicationConfiguration.CertificateValidator.CertificateValidation -= OnCertificateValidation;
        applicationConfiguration.CertificateValidator.CertificateValidation += OnCertificateValidation;
        applicationConfiguration.CertificateValidator.UpdateAsync(applicationConfiguration).GetAwaiter().GetResult();

        GatewayLogMessages.TrustPolicyDefaultsApplied(
            _logger,
            securityConfiguration.AutoAcceptUntrustedCertificates,
            securityConfiguration.RejectSHA1SignedCertificates,
            securityConfiguration.MinimumCertificateKeySize);
    }

    private void OnCertificateValidation(CertificateValidator sender, CertificateValidationEventArgs eventArgs)
    {
        if (eventArgs.Error.StatusCode != StatusCodes.BadCertificateUntrusted)
        {
            return;
        }

        eventArgs.Accept = false;

        GatewayLogMessages.UntrustedCertificateRejected(
            _logger,
            eventArgs.Certificate?.Thumbprint ?? "unknown",
            eventArgs.Error.StatusCode.Code);
    }

    private void PublishStartupHealth(StartupHealthStatus status, string reason, string correlationId)
    {
        var snapshot = _startupHealthState.Update(status, reason, correlationId);
        GatewayLogMessages.StartupHealthStateChanged(
            _logger,
            snapshot.Status.ToString(),
            snapshot.Reason,
            snapshot.CorrelationId ?? correlationId);
    }

    private UpstreamEndpointConfigurationDocument LoadValidatedUpstreamEndpointConfiguration()
    {
        var document = UpstreamEndpointConfigurationStore.LoadOrCreateDefault();
        var enabledEndpointCount = document.Endpoints.Count(endpoint => endpoint.Enabled);

        GatewayLogMessages.UpstreamEndpointConfigurationLoaded(
            _logger,
            document.Endpoints.Count,
            enabledEndpointCount);

        var issues = UpstreamEndpointConfigurationValidator.Validate(document);

        if (issues.Count == 0)
        {
            return document;
        }

        foreach (var issue in issues)
        {
            GatewayLogMessages.UpstreamEndpointConfigurationValidationFailed(
                _logger,
                issue.EndpointId,
                issue.Message);
        }

        throw new InvalidOperationException(
            "Upstream endpoint configuration failed validation. Fix config/upstream-endpoints.json and restart.");
    }

    private LocalServerConfigurationDocument LoadValidatedLocalServerConfiguration()
    {
        var document = LocalServerConfigurationStore.LoadOrCreateDefault();
        var issues = LocalServerConfigurationValidator.Validate(document);

        if (issues.Count == 0)
        {
            return document;
        }

        foreach (var issue in issues)
        {
            _logger.LogError(
                "Local server configuration validation failed. Setting: {Setting}, Issue: {Issue}",
                issue.Setting,
                issue.Message);
        }

        throw new InvalidOperationException(
            "Local server configuration failed validation. Fix config/server-settings.json and restart.");
    }

    private NamespaceMappingConfigurationDocument LoadValidatedMappingConfiguration(
        UpstreamEndpointConfigurationDocument upstreamEndpointConfiguration)
    {
        var mapping = NamespaceMappingConfigurationStore.LoadOrCreateDefault();
        GatewayLogMessages.NamespaceMappingLoaded(_logger, mapping.Rules.Count);

        var issues = NamespaceMappingConfigurationValidator.Validate(mapping, upstreamEndpointConfiguration);
        if (issues.Count == 0)
        {
            return mapping;
        }

        foreach (var issue in issues)
        {
            GatewayLogMessages.NamespaceMappingValidationFailed(_logger, issue.EndpointId, issue.Message);
        }

        throw new InvalidOperationException(
            "Namespace mapping configuration failed validation. Fix config/namespace-mapping.json and restart.");
    }

    private void PublishSecurityDiagnosticsSnapshot(SecurityBootstrapDiagnosticsSnapshot snapshot)
    {
        SecurityBootstrapDiagnosticsStore.Save(snapshot);
        GatewayLogMessages.SecurityDiagnosticsPublished(_logger, SecurityBootstrapDiagnosticsStore.DiagnosticsFilePath);
    }

    public void Stop()
    {
        StopLocalServerEndpoint();
    }

    private void StartLocalServerEndpoint(
        ApplicationConfiguration applicationConfiguration,
        NamespaceMappingConfigurationDocument mappingConfiguration,
        string correlationId)
    {
        if (_localServerApplicationInstance is not null)
        {
            return;
        }

        GatewayLogMessages.LocalServerStartRequested(_logger, correlationId);

        try
        {
            var telemetryContext = DefaultTelemetry.Create(_ => { });

            _localServer = new UAGatewayLocalServer(mappingConfiguration, projectedEndpointCount =>
            {
                GatewayLogMessages.NamespaceProjectionBuilt(_logger, projectedEndpointCount);
            });
            _localServerApplicationInstance = new ApplicationInstance(applicationConfiguration, telemetryContext)
            {
                ApplicationName = applicationConfiguration.ApplicationName,
                ApplicationType = applicationConfiguration.ApplicationType,
            };

            _localServerApplicationInstance.StartAsync(_localServer).GetAwaiter().GetResult();

            var baseAddress = applicationConfiguration.ServerConfiguration.BaseAddresses.FirstOrDefault() ?? "unknown";
            GatewayLogMessages.LocalServerStarted(_logger, baseAddress);
        }
        catch (Exception ex)
        {
            GatewayLogMessages.LocalServerStartFailed(_logger, ex.Message, ex);
            var baseAddress = applicationConfiguration.ServerConfiguration.BaseAddresses.FirstOrDefault() ?? "unknown";
            var startupMessage = IsListenerPortConflict(ex)
                ? $"Local OPC UA server endpoint failed to start because the configured listener address {baseAddress} is already in use. Stop the existing service instance or change the configured host/port, then retry. Check logs for event IDs 5000-5002."
                : $"Local OPC UA server endpoint failed to start for configured address {baseAddress}. Check logs for event IDs 5000-5002.";

            throw new InvalidOperationException(
                startupMessage,
                ex);
        }
    }

    private static bool IsListenerPortConflict(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            var message = current.Message;

            if (message.Contains("Failed to establish tcp listener sockets", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("address already in use", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("Only one usage of each socket address", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("The requested address is not valid in its context", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private void StopLocalServerEndpoint()
    {
        if (_localServerApplicationInstance is null)
        {
            return;
        }

        GatewayLogMessages.LocalServerStopRequested(_logger);

        try
        {
            _localServerApplicationInstance.StopAsync().GetAwaiter().GetResult();
        }
        finally
        {
            _localServer = null;
            _localServerApplicationInstance = null;
            GatewayLogMessages.LocalServerStopped(_logger);
        }
    }
}
