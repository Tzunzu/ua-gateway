# UA Gateway Help

## Who this guide is for

This guide is for operators and developers who use the UA Gateway UI and service in local or lab environments.

See also:
- [Operations and Developer Guide](OPERATIONS_AND_DEV_GUIDE.md) — full setup and run instructions
- [Architecture overview](ARCHITECTURE.md) — how the components fit together
- [OPC UA Foundation](https://opcfoundation.org) — the upstream protocol standard
- [Windows App SDK docs](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/) — UI framework reference

## What UA Gateway does

UA Gateway connects to one or more upstream OPC UA endpoints and exposes a single local OPC UA endpoint for downstream clients.

## Before you start

- OS: Windows 10 or Windows 11
- .NET SDK: 8.x
- Build once before first run:
  - `dotnet restore UAGateway.sln`
  - `dotnet build UAGateway.sln`

## Start and stop

From VS Code tasks:

- `Run UAGateway Service`: starts the service project
- `Run UAGateway UI`: starts the UI
- `Run UAGateway Service + UI`: starts both
- `Stop Existing UAGateway Service`: stops running service processes from this workspace

Notes:

- Service debug launch is configured to stop old service processes before starting a new one.
- If settings are changed in the UI, restart the service to apply listener address changes.

## UI overview

### Dashboard

Shows service startup state, security bootstrap diagnostics, and connection snapshot data.

### Connections

Use this tab to manage upstream endpoint configuration.

Typical flow:

1. Add or edit connection entries.
2. Select `Apply Configuration`.
3. Check status text for validation or save results.

If you have unsaved edits and press reload, the UI asks whether to keep editing or replace local edits.

### Server Settings

Use this tab to configure the local OPC UA listener address.

Fields:

- Host: host or IP value only (for example `localhost` or `127.0.0.1`)
- Port: TCP port in range 1 to 65535
- Endpoint path: endpoint path segment (for example `UAGateway`)

Buttons:

- `Reload Configuration`: reloads from `%ProgramData%\UA Gateway\config\server-settings.json`
- `Restore Defaults`: resets editor values to defaults
- `Apply Configuration`: saves settings to disk

Defaults:

- Host: `localhost`
- Port: `4840`
- Endpoint path: `UAGateway`

After apply, restart the service to bind the new listener address.

### Logs

Displays service logs with filter options for operator troubleshooting.

### Live Output

Shows append-only live service events from the event stream.

## Service status meanings

Bottom status bar values:

- `Connected`: startup and runtime checks are nominal
- `Limited`: service is running but one or more checks are degraded
- `Failed`: startup faulted and service is not healthy
- `Offline`: UI cannot reach service IPC

## Configuration files

Directory:

`%ProgramData%\UA Gateway\config`

Files:

- `upstream-endpoints.json`
- `namespace-mapping.json`
- `server-settings.json`

## Common problems

### Listener address already in use

Symptoms:

- Startup fails with a local server listener address conflict message

Fix:

1. Run `Stop Existing UAGateway Service`.
2. Restart service.
3. If needed, set a different host or port in `Server Settings` and apply.
4. Restart service again.

### Invalid server settings JSON

Symptoms:

- Startup fault reason mentions invalid local server settings JSON

Fix:

1. Open `%ProgramData%\UA Gateway\config\server-settings.json`.
2. Correct JSON syntax.
3. Restart service.

### Invalid upstream or mapping configuration

Symptoms:

- Startup fault reason mentions invalid upstream endpoint config or namespace mapping config

Fix:

1. Correct the referenced file under `%ProgramData%\UA Gateway\config`.
2. Restart service.

## Validation checklist

After configuration changes:

1. Run `dotnet build UAGateway.sln`.
2. Run `dotnet test UAGateway.sln`.
3. Start service and UI.
4. Confirm status bar and Dashboard reflect expected state.
5. Verify downstream clients can reach the configured local endpoint.
