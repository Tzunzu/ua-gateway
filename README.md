# UA Gateway

> [!WARNING]
> This project is under active development and is not in a working production-ready state yet.

UA Gateway is an open-source Windows OPC UA gateway for PLC and SCADA environments. It connects to one or more PLCs or upstream OPC UA sources and exposes a single local OPC UA server endpoint for SCADA systems, historians, and visualization tools.

## Solution layout

- `src/UAGateway.Core` - shared OPC UA routing, mapping, and configuration logic
- `src/UAGateway.Service` - Windows Service host that manages upstream connections and the local OPC UA server
- `src/UAGateway.UI` - Windows-only desktop UI for monitoring and configuration (WinUI 3)
- `tests/UAGateway.Core.Tests` - core logic tests

## Design goals

- One network-facing connection point for downstream SCADA clients
- Multiple upstream PLC and OPC UA connections behind the gateway
- Secure defaults and Windows Service deployment
- Clear separation between runtime, UI, and shared logic

## UI platform strategy

- `UAGateway.UI` is intentionally WinUI 3 and Windows-only.
- UI remains separate from `UAGateway.Core` and `UAGateway.Service` so an additional Linux-compatible UI can be created later without changing runtime logic.

## Branding note

Current product name is `UA Gateway`. `RelayForge` is reserved as a possible future umbrella brand.

## Continuity docs

For continuing this project on another computer or session, start with:

- [Project context](docs/PROJECT_CONTEXT.md)
- [Architecture](docs/ARCHITECTURE.md)
- [Decisions](docs/DECISIONS.md)
- [V1 scope contract](docs/V1_SCOPE.md)
- [Priorities](docs/PRIORITIES.md)
- [Tooling, debug, and event plan](docs/TOOLING_DEBUG_EVENT_PLAN.md)
- [Debug runbook](docs/DEBUG_RUNBOOK.md)
- [Operations and developer guide](docs/OPERATIONS_AND_DEV_GUIDE.md)
- [Implementation tracker](docs/IMPLEMENTATION_TRACKER.md)
- [Roadmap](docs/ROADMAP.md)

## Next steps

- Add the OPC UA stack and service hosting packages
- Implement connection management and mapping models
- Add configuration persistence and UI/service IPC
