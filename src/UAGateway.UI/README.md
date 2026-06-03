# UAGateway.UI

This project is the Windows frontend for UA Gateway and is built with WinUI 3.

## Scope

- Windows-only operator UI
- Monitoring, configuration, and diagnostics views
- No runtime tunnel logic (that belongs in UAGateway.Core and UAGateway.Service)

## Cross-platform note

Linux-compatible UI should be implemented as a separate frontend project that consumes the same service/core contracts.
