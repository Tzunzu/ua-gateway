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

## Concurrency Direction

- Prefer async/event-driven architecture
- Use dedicated workers only where blocking operations require it
- Keep connection/session state management centralized

## Security Direction

- Enforce secure endpoint policies by default
- Manage trust lists and certificates explicitly
- Keep credentials and sensitive configuration outside UI-specific state
