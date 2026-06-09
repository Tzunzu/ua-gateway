# UAGateway.UI

This project is the Windows frontend for UA Gateway and is built with WinUI 3.

## Scope

- Windows-only operator UI
- Monitoring, configuration, and diagnostics views
- No runtime tunnel logic (that belongs in UAGateway.Core and UAGateway.Service)

## Shell structure

- Root shell uses WinUI `NavigationView` + `Frame` with route-based page navigation.
- Primary routes: Dashboard, Connections, Server Settings, Logs, Live Output, and Settings.
- Settings route is the command destination for theme/palette management; Help is available in shell header and Settings.
- UI IPC/status orchestration is centralized in `Services/ShellStateService.cs`; `MainWindow` stays focused on shell composition.

## Cross-platform note

Linux-compatible UI should be implemented as a separate frontend project that consumes the same service/core contracts.
