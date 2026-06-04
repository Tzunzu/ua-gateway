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
- `namespace-mapping.json`: mirror/rename mapping rules

### Upstream Endpoint Example

```json
{
  "endpoints": [
    {
      "id": "plc-1",
      "displayName": "PLC 1",
      "endpointUrl": "opc.tcp://127.0.0.1:4841",
      "enabled": true
    }
  ]
}
```

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
- Connections: draft endpoint add/reload/apply flow with validation feedback
- Logs: severity, category text, and event text filtering over service log lines

## Troubleshooting Checklist

1. Build and test first.
2. Confirm config JSON validates.
3. Confirm logs exist in `%ProgramData%\UA Gateway\logs`.
4. Check event IDs around startup and reconnect lifecycle.
5. Verify diagnostics snapshot files update after service start.
