# Roadmap

## Current Stage

Scaffold and foundation. Build and test are running. Runtime bootstrap is minimal.

## Near-Term Milestones

1. OPC UA Runtime Bootstrap
- Add full ApplicationConfiguration
- Add certificate store and trust setup
- Validate startup configuration path

2. Connection Management
- Add upstream endpoint configuration model
- Implement connect/reconnect lifecycle handling
- Add health status model

3. Local Server Exposure
- Stand up local OPC UA server endpoint
- Define and host initial namespace structure

4. UI Shell and Operations Views
- Create navigation shell (WinUI 3)
- Add Dashboard and Connections views
- Show service state and connection health

5. Service/UI Contract
- Define initial API/IPC contract for status and config
- Implement safe apply/reload workflow

## Release Readiness Themes

- Security hardening and certificate UX
- Operational diagnostics and logging quality
- Test coverage for mapping and connection lifecycle
- Documentation quality for contributors and operators
