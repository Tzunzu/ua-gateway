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

                var applicationConfiguration = BuildApplicationConfiguration();
                var upstreamEndpointConfiguration = LoadValidatedUpstreamEndpointConfiguration();
                var mappingConfiguration = LoadValidatedMappingConfiguration(upstreamEndpointConfiguration);
                var enabledEndpointCount = upstreamEndpointConfiguration.Endpoints.Count(endpoint => endpoint.Enabled);
                _connectionLifecycleManager.SetApplicationConfiguration(applicationConfiguration);
                _connectionLifecycleManager.ApplyConfiguration(upstreamEndpointConfiguration);

                ValidateApplicationConfiguration(applicationConfiguration);
                GatewayLogMessages.OpcUaConfigurationValidated(
                    _logger,
                    applicationConfiguration.ApplicationUri ?? "unknown");

                InitializeCertificateStores(applicationConfiguration);
                StartLocalServerEndpoint(applicationConfiguration, mappingConfiguration, configApplyCorrelationId);

                // This confirms the OPC UA stack package is available and wired into the service.
                var statusCode = StatusCodes.Good;
                GatewayLogMessages.OpcUaBootstrapInitialized(_logger, statusCode);

                PublishStartupHealth(
                    StartupHealthStatus.Healthy,
                    "Configuration and security bootstrap completed.",
                    configApplyCorrelationId);

                if (enabledEndpointCount == 0)
                {
                    PublishStartupHealth(
                        StartupHealthStatus.Degraded,
                        "Startup completed without configured upstream endpoints.",
                        configApplyCorrelationId);
                }

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
            PublishStartupHealth(
                StartupHealthStatus.Faulted,
                $"Startup failed: {ex.Message}",
                faultCorrelationId);

            PublishSecurityDiagnosticsSnapshot(new SecurityBootstrapDiagnosticsSnapshot(
                SecurityBootstrapStatus.Faulted,
                $"Startup failed: {ex.Message}",
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

    private static string CreateCorrelationId()
    {
        return Guid.NewGuid().ToString("N");
    }

    private static ApplicationConfiguration BuildApplicationConfiguration()
    {
        var hostName = Utils.GetHostName();
        var appDataRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "UA Gateway");
        var pkiRoot = Path.Combine(appDataRoot, "pki");

        var ownStorePath = Path.Combine(pkiRoot, "own");
        var trustedStorePath = Path.Combine(pkiRoot, "trusted");
        var issuerStorePath = Path.Combine(pkiRoot, "issuer");
        var rejectedStorePath = Path.Combine(pkiRoot, "rejected");

        Directory.CreateDirectory(ownStorePath);
        Directory.CreateDirectory(trustedStorePath);
        Directory.CreateDirectory(issuerStorePath);
        Directory.CreateDirectory(rejectedStorePath);

        return new ApplicationConfiguration
        {
            ApplicationName = "UA Gateway",
            ApplicationUri = $"urn:{hostName}:UAGateway:Service",
            ProductUri = "urn:uagateway:service",
            ApplicationType = ApplicationType.ClientAndServer,
            SecurityConfiguration = new SecurityConfiguration
            {
                ApplicationCertificate = new CertificateIdentifier
                {
                    StoreType = CertificateStoreType.Directory,
                    StorePath = ownStorePath,
                    SubjectName = $"CN=UA Gateway, DC={hostName}"
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
                    "opc.tcp://localhost:4840/UAGateway",
                },
                SecurityPolicies = new ServerSecurityPolicyCollection
                {
                    new ServerSecurityPolicy
                    {
                        SecurityMode = MessageSecurityMode.SignAndEncrypt,
                        SecurityPolicyUri = SecurityPolicies.Basic256Sha256,
                    },
                },
                UserTokenPolicies = new UserTokenPolicyCollection
                {
                    new UserTokenPolicy(UserTokenType.Anonymous),
                    new UserTokenPolicy(UserTokenType.UserName),
                },
                DiagnosticsEnabled = true,
            },
            DisableHiResClock = false,
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

    private void InitializeCertificateStores(ApplicationConfiguration applicationConfiguration)
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

                PublishSecurityDiagnosticsSnapshot(new SecurityBootstrapDiagnosticsSnapshot(
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
                    applicationConfiguration.SecurityConfiguration.MinimumCertificateKeySize));
            }
            catch (Exception ex)
            {
                GatewayLogMessages.SecurityBootstrapFailed(_logger, ex.Message, ex);

                PublishSecurityDiagnosticsSnapshot(new SecurityBootstrapDiagnosticsSnapshot(
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
                    2048));

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
            throw new InvalidOperationException(
                "Local OPC UA server endpoint failed to start. Check logs for event IDs 5000-5002.",
                ex);
        }
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
