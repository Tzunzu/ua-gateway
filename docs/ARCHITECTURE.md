# Architecture

## High-Level Components

- UAGateway.Core: shared routing, mapping, and configuration domain logic
- UAGateway.Service: Windows Service runtime host
- UAGateway.UI: Windows-only WinUI 3 operations frontend
- UAGateway.Core.Tests: tests for core behavior

## Runtime Model

The service acts in two roles:

- OPC UA client to upstream PLCs/servers
- OPC UA server for downstream SCADA/historian clients

This enables one downstream endpoint while maintaining many upstream connections.

## Process and Boundary Rules

- Service and Core contain runtime and protocol logic
- UI is intentionally separate and should use service-facing contracts
- Linux-compatible UI should be implemented as a separate frontend project

## UI Shell Notes

- The WinUI shell uses `NavigationView` + `Frame` with deterministic route keys.
- Page wrappers host operator controls under `src/UAGateway.UI/Pages` to keep migration risk low.
- IPC handshake, snapshot refresh, and shell status evaluation are centralized in `ShellStateService`.
- The Settings route owns shell command placement for theme/palette configuration.

## Connections Editor Layout Pattern

- The Connections settings surface uses reusable custom controls:
- `SettingsSectionCard` for bordered/labeled sections.
- `SettingsFieldBox` for labeled field containers with consistent width behavior.
- Section internals use `ItemsControl` with `WrapGrid` for compact, responsive flow wrapping.
- Field sizing and spacing constants are centralized in `ConnectionsEditor.xaml` resources (`SettingFieldCompactWidth`, `SettingFieldStandardWidth`, `SettingFieldWideWidth`, `SettingComboMinWidth`, `SettingFieldMargin`).
- New settings should reuse these controls and constants before introducing new ad-hoc grids/stack panels.

## Concurrency Direction

- Prefer async/event-driven architecture
- Use dedicated workers only where blocking operations require it
- Keep connection/session state management centralized

## Security Direction

- Enforce secure endpoint policies by default
- Manage trust lists and certificates explicitly
- Keep credentials and sensitive configuration outside UI-specific state
- Persist endpoint non-secret settings in `upstream-endpoints.json` and store endpoint secrets in an encrypted credential store (`upstream-endpoint-credentials.json`, DPAPI on Windows)

## Connection Settings Ownership

- Core owns endpoint configuration schema and validation for security, authentication, transport, and retry settings.
- Service owns runtime interpretation of endpoint settings (probe timeout, retry scheduling, reconnect gating, subscription tuning values, and security expectation checks).
- UI owns operator editing workflows and writes secrets through the credential store API instead of writing plaintext credentials into endpoint configuration.

## Local Server Settings Ownership

- Core owns local server configuration schema and validation, including listener endpoint values plus server identity/security/token policy settings.
- Service owns runtime mapping of local server settings into OPC UA `ApplicationConfiguration` and `ServerConfiguration` at startup.
- UI owns operator editing flows (reload/default/apply, preview/status messaging) and should not embed runtime OPC UA defaults that duplicate Core/Service ownership.
