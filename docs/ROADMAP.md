# Roadmap

## Current Stage

Scaffold and foundation. Build and test are running. Runtime bootstrap is minimal.

Next stage: second-pass implementation. The baseline milestone work should now be hardened into a real V1-quality implementation with explicit runtime verification, failure-path handling, and regression coverage.

## Near-Term Milestones

1. Tooling and debug foundation
- Lock the local engineering loop (restore/build/test/run)
- Define initial structured logging categories and event ID ranges
- Create baseline troubleshooting workflow and documentation

2. OPC UA Runtime Bootstrap
- Add full ApplicationConfiguration
- Add certificate store and trust setup
- Validate startup configuration path

3. Connection Management
- Add upstream endpoint configuration model
- Implement connect/reconnect lifecycle handling
- Add health status model

4. Local Server Exposure
- Stand up local OPC UA server endpoint
- Define and host initial namespace structure

5. UI Shell and Operations Views
- Create navigation shell (WinUI 3)
- Add Dashboard and Connections views
- Show service state and connection health

6. Service/UI Contract
- Define initial API/IPC contract for status and config
- Implement safe apply/reload workflow
- Draft contract and phased rollout are tracked in SERVICE_UI_IPC_CONTRACT_DRAFT.md

## Release Readiness Themes

- Security hardening and certificate UX
- Operational diagnostics and logging quality
- Test coverage for mapping and connection lifecycle
- Documentation quality for contributors and operators

## Second-Pass Direction

1. UI structure first
- Lock the WinUI shell, layout, navigation, and operator workflows early.
- Use the UI pass to clarify goals, state boundaries, and missing service contracts.

2. Reliability hardening immediately after UI structure
- Revisit runtime bootstrap, certificate/trust handling, connection lifecycle, and local server behavior.
- Treat scaffold implementations as incomplete until failure paths and recovery paths are verified.

3. Mapping, projection, and release baseline
- Harden mapping and projection refresh behavior.
- Expand regression tests and intentional publish workflows.
- Align docs with actual implementation and troubleshooting practice.
