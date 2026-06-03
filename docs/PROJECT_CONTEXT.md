# Project Context

## Status

This project is under active development and is not yet production-ready.

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
