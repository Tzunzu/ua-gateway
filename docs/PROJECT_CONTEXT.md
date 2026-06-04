# Project Context

## Status

This project is under active development and is not yet production-ready.

Current engineering checkpoint (2026-06-04):
- Milestone M6 (runtime bootstrap and security hardening) is complete.
- Build and test baseline is green after M6 changes.
- Milestone M7 is intentionally deferred for now due AI budget constraints.
- Planned resume point: M7-01 connection lifecycle state-machine formalization.

## Product Summary

UA Gateway is an open-source Windows OPC UA gateway for PLC and SCADA environments.

The gateway connects to one or more PLCs or upstream OPC UA sources and exposes a single local OPC UA server endpoint for SCADA systems, historians, and visualization clients.

## Why This Exists

- Simplify firewall configuration by exposing one controlled connection point
- Reduce attack surface by limiting direct downstream access to PLC networks
- Centralize connection policy, diagnostics, and operational visibility

## Core Principles

- Keep runtime logic in service/core layers, not in UI
- Keep the UI replaceable (Windows now, other frontends later)
- Use secure defaults and explicit trust/certificate handling
- Prioritize operational reliability and diagnosability

## Naming

- Product display name: UA Gateway
- Technical naming convention: UAGateway.*
- RelayForge is reserved as a possible future umbrella brand
