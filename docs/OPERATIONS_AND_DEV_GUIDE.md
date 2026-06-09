# Operations and Developer Guide

## Scope

This guide covers baseline local development and operator workflows for UA Gateway.

For day-to-day operator usage and UI workflows, start with `docs/HELP.md`.

## Prerequisites

- Windows 10/11
- .NET SDK 8.x
- VS Code with C# extension

## Local Tasks

Use the VS Code tasks provided in this repository:

- Restore: `Restore UAGateway`
- Build: `Build UAGateway`
- Test: `Test UAGateway`
- Run service: `Run UAGateway Service`
- Run UI: `Run UAGateway UI`

## Pre-push Reminder Setup

Enable repository-managed hooks once per clone:

`git config core.hooksPath .githooks`

After this, each push shows a checklist prompt before continuing.

Checklist reference:

- `docs/PUSH_CHECKLIST.md`

One-time bypass (use rarely):

`SKIP_PUSH_CHECK=1 git push`

## Baseline Runtime Behavior

At startup the service:

1. Builds and validates OPC UA application configuration.
2. Initializes certificates and trust policy defaults.
3. Loads upstream endpoint config and namespace mapping config.
4. Starts local OPC UA server endpoint.
5. Starts baseline reconnect lifecycle attempts for enabled upstream endpoints.

## Configuration Files

The service reads and writes config under:

`%ProgramData%\UA Gateway\config`

Files:

- `upstream-endpoints.json`: upstream endpoint definitions
- `upstream-endpoint-credentials.json`: encrypted endpoint credentials (DPAPI, Windows user scope)
- `namespace-mapping.json`: mirror/rename mapping rules
- `server-settings.json`: local OPC UA listener, server identity, and server-side security/token policy settings

### Local Server Settings Example

```json
{
  "host": "localhost",
  "port": 4840,
  "endpointPath": "UAGateway",
  "applicationName": "UA Gateway",
  "productUri": "urn:uagateway:service",
  "securityMode": "SignAndEncrypt",
  "securityPolicy": "Basic256Sha256",
  "allowAnonymous": true,
  "allowUsernamePassword": true
}
```

Local server settings notes:

- `securityMode`: `None`, `Sign`, or `SignAndEncrypt`
- `securityPolicy`: `None`, `Basic128Rsa15`, `Basic256`, `Basic256Sha256`, `Aes128Sha256RsaOaep`, `Aes256Sha256RsaPss`
- At least one token policy must be enabled (`allowAnonymous` or `allowUsernamePassword`)
- Listener and server identity/security changes are saved immediately but still require service restart to rebind/reinitialize runtime server config

### Upstream Endpoint Example

```json
{
  "endpoints": [
    {
      "id": "plc-1",
      "displayName": "PLC 1",
      "endpointUrl": "opc.tcp://127.0.0.1:4841",
      "enabled": true,
      "security": {
        "securityMode": "SignAndEncrypt",
        "securityPolicy": "Basic256Sha256",
        "autoAcceptUntrustedCertificates": false
      },
      "authentication": {
        "mode": "Anonymous",
        "credentialId": ""
      },
      "transport": {
        "connectionTimeoutMs": 5000,
        "operationTimeoutMs": 15000,
        "sessionTimeoutMs": 60000
      },
      "subscription": {
        "publishingIntervalMs": 1000,
        "samplingIntervalMs": 1000,
        "queueSize": 100,
        "maxItemsPerSubscription": 500,
        "keepAliveCount": 10,
        "lifetimeCount": 30,
        "maxNotificationsPerPublish": 0,
        "publishingEnabled": true,
        "priority": 0,
        "discardOldest": true
      },
      "retry": {
        "strategy": "Exponential",
        "initialDelaySeconds": 2,
        "maxDelaySeconds": 60,
        "successProbeIntervalSeconds": 30,
        "maxAttempts": 0,
        "reconnectOnFailure": true
      }
    }
  ]
}
```

Credential handling notes:

- Username/password mode requires `authentication.credentialId` in `upstream-endpoints.json`.
- UI stores username/password secrets in `upstream-endpoint-credentials.json`.
- Secrets are encrypted with DPAPI for the current Windows user profile and are not stored in plaintext inside `upstream-endpoints.json`.

### Namespace Mapping Example

```json
{
  "rules": [
    {
      "endpointId": "plc-1",
      "projectedName": "BoilerPLC",
      "enabled": true
    }
  ]
}
```

## Diagnostics Output

Logs directory:

`%ProgramData%\UA Gateway\logs`

Diagnostics snapshots directory:

`%ProgramData%\UA Gateway\diagnostics`

Files:

- `security-bootstrap.json`
- `connection-lifecycle.json`

## UI Baseline Views

- Dashboard: security and connection diagnostics snapshots
- Connections: full-height two-panel editor with draft endpoint add/reload/apply flow and standard OPC UA connection settings
- Server Settings: full-height two-panel editor with local listener settings, server identity/security/token policy controls, endpoint preview, and draft apply/reload/default workflow
- Logs: severity, category text, and event text filtering over service log lines

## Troubleshooting Checklist

1. Build and test first.
2. Confirm config JSON validates.
3. Confirm logs exist in `%ProgramData%\UA Gateway\logs`.
4. Check event IDs around startup and reconnect lifecycle.
5. Verify diagnostics snapshot files update after service start.
