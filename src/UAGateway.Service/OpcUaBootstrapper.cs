using Microsoft.Extensions.Logging;
using Opc.Ua;
using Opc.Ua.Configuration;
using UAGateway.Core.Health;

namespace UAGateway.Service;

internal sealed class OpcUaBootstrapper
{
    private readonly ILogger<OpcUaBootstrapper> _logger;
    private readonly StartupHealthState _startupHealthState;

    public OpcUaBootstrapper(ILogger<OpcUaBootstrapper> logger, StartupHealthState startupHealthState)
    {
        _logger = logger;
        _startupHealthState = startupHealthState;
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

                ValidateApplicationConfiguration(applicationConfiguration);
                GatewayLogMessages.OpcUaConfigurationValidated(
                    _logger,
                    applicationConfiguration.ApplicationUri ?? "unknown");

                InitializeCertificateStores(applicationConfiguration);

                // This confirms the OPC UA stack package is available and wired into the service.
                var statusCode = StatusCodes.Good;
                GatewayLogMessages.OpcUaBootstrapInitialized(_logger, statusCode);

                PublishStartupHealth(
                    StartupHealthStatus.Healthy,
                    "Configuration and security bootstrap completed.",
                    configApplyCorrelationId);

                GatewayLogMessages.ConfigApplyCompleted(_logger, configApplyCorrelationId);
            }

            var reconnectCorrelationId = CreateCorrelationId();

            using (_logger.BeginScope("CorrelationId:{CorrelationId}", reconnectCorrelationId))
            {
                GatewayLogMessages.ReconnectFlowStarted(_logger, reconnectCorrelationId);
                GatewayLogMessages.ConnectionManagerInitialized(_logger);
                GatewayLogMessages.NoUpstreamEndpointsConfigured(_logger, reconnectCorrelationId);
                GatewayLogMessages.ReconnectFlowIdleNoEndpoints(_logger, reconnectCorrelationId);

                PublishStartupHealth(
                    StartupHealthStatus.Degraded,
                    "Startup completed without configured upstream endpoints.",
                    reconnectCorrelationId);
            }
        }
        catch (Exception ex)
        {
            var faultCorrelationId = CreateCorrelationId();
            PublishStartupHealth(
                StartupHealthStatus.Faulted,
                $"Startup failed: {ex.Message}",
                faultCorrelationId);

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
            }
            catch (Exception ex)
            {
                GatewayLogMessages.SecurityBootstrapFailed(_logger, ex.Message, ex);
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
}
