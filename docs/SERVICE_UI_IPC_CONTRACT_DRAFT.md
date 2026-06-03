# Service/UI IPC Contract Draft

Status: Draft for second-pass implementation planning.

## Goals

- Provide a stable local contract between UAGateway.Service and UAGateway.UI.
- Support request/reply operations for snapshots and commands.
- Support a live event stream for testing, diagnostics, and operator visibility.
- Keep transport replaceable (Windows-first now, cross-platform-friendly later).

## Non-Goals (for initial pass)

- Cross-machine remote control.
- Cloud control plane integration.
- Enterprise RBAC and multi-tenant auth model.

## Recommended Transport Direction

Primary (Windows-first): named pipes with request/reply plus stream channels.

Future-friendly abstraction: keep IPC behind an interface so transport can move to gRPC later without changing UI feature modules.

## Contract Versioning

- protocolVersion string in handshake response (example: 1.0).
- Capability flags in handshake response for optional features.
- Backward-compatible additive changes only within a major version.

## Connection Model

Three logical channels (may share one physical transport with multiplexing):

1. Control channel
- Request/reply for snapshot queries and commands.

2. Event channel
- Server-to-client stream of lifecycle and diagnostics events.

3. Optional bulk channel
- Future large payloads (export/import, bundle diagnostics).

## Core Envelopes

### Request envelope

```json
{
  "requestId": "8f21f49a-0d6f-4b6b-9ed8-8df27ec9f4b2",
  "timestampUtc": "2026-06-03T18:05:12.112Z",
  "method": "connections.getSnapshot",
  "payload": {},
  "client": {
    "name": "UAGateway.UI",
    "version": "0.1.0"
  }
}
```

### Response envelope

```json
{
  "requestId": "8f21f49a-0d6f-4b6b-9ed8-8df27ec9f4b2",
  "timestampUtc": "2026-06-03T18:05:12.146Z",
  "success": true,
  "errorCode": null,
  "message": null,
  "payload": {}
}
```

### Event envelope

```json
{
  "eventId": "7b4fe2e4-f8ab-49a7-9f93-5ea2a83f897a",
  "timestampUtc": "2026-06-03T18:05:13.001Z",
  "category": "connection.lifecycle",
  "name": "EndpointReconnecting",
  "severity": "Warning",
  "serviceEventId": 2004,
  "correlationId": "cfg-apply-20260603-180510",
  "payload": {}
}
```

## Handshake

Method: system.handshake

Request payload:

```json
{
  "requestedProtocolVersion": "1.0",
  "subscribeEvents": true
}
```

Response payload:

```json
{
  "protocolVersion": "1.0",
  "serviceVersion": "0.1.0",
  "capabilities": {
    "eventStream": true,
    "applyConfig": true,
    "liveLogEvents": true,
    "securityActions": false
  }
}
```

## Snapshot Methods (initial)

1. health.getStartup
- Returns current startup health model (healthy/degraded/faulted plus reason).

2. security.getBootstrap
- Returns security bootstrap diagnostics snapshot.

3. connections.getSnapshot
- Returns endpoint lifecycle summary plus per-endpoint states and counters.

4. connections.getDraftConfig
- Returns current upstream endpoint configuration document from service-side source of truth.

5. mapping.getSnapshot
- Returns mapping rule snapshot and current projection summary.

## Command Methods (initial)

1. connections.applyDraftConfig
- Validates and applies endpoint draft configuration.
- Response includes correlationId and validation issues.

2. connections.reloadDraftConfig
- Reloads draft from persisted store.

3. runtime.requestReconnect
- Requests reconnect for one endpoint or all enabled endpoints.

4. diagnostics.requestSnapshotRefresh
- Requests immediate diagnostics snapshot write/update.

## Event Categories (initial)

1. service.lifecycle
- Startup begin/complete/faulted, shutdown begin/complete.

2. security.bootstrap
- Certificate bootstrap started/completed/failed, trust changes.

3. connection.lifecycle
- Connecting/connected/disconnected/reconnecting/failure.

4. config.apply
- Apply requested/validated/applied/failed (with correlation).

5. server.endpoint
- Local server start/stop/fault events.

6. diagnostics.log
- Stream-ready log summary events for Live Output tab.

## Error Model

Common errorCode values:

- ProtocolVersionUnsupported
- MethodNotFound
- ValidationFailed
- Conflict
- ServiceUnavailable
- Timeout
- InternalError

Validation failure response payload shape:

```json
{
  "correlationId": "cfg-apply-20260603-180510",
  "issues": [
    {
      "code": "EndpointUrlInvalid",
      "target": "endpoints[2].endpointUrl",
      "message": "Endpoint URL scheme must be opc.tcp."
    }
  ]
}
```

## Testing Value of Event Stream

The event stream should be first-class during testing because it allows:

- deterministic assertions on runtime transitions,
- correlation-based tracing for apply/reconnect workflows,
- faster root-cause analysis than file polling only,
- UI integration tests that validate real state transitions.

## Implementation Phasing

Phase 1: Contract skeleton and handshake
- Add shared message envelopes and handshake endpoint.
- Add basic snapshot request/reply support.

Phase 2: Event stream
- Publish service lifecycle and connection lifecycle events.
- Expose event stream in UI Live Output tab (stream-first, file-tail fallback).

Phase 3: Commands
- Implement apply/reload/reconnect commands with correlation IDs.
- Surface validation issues and completion status in UI.

Phase 4: Hardening
- Add timeout/retry policy, disconnect handling, and reconnection behavior.
- Add regression tests for reconnect and apply race scenarios.

## Open Decisions

1. Exact transport implementation for V1
- Raw named pipes vs gRPC over named pipes.

2. Serialization format
- JSON for ease of debugging vs binary for throughput.

3. Stream backpressure policy
- Drop oldest, coalesce, or block producer under heavy load.

4. Security scope for local IPC
- Service account ACL policy and allowed client identity rules.
