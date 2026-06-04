# Implementation Tracker

Last Updated: 2026-06-04

## How to use this file

- Track progress by task ID.
- Keep one task marked In Progress at a time.
- Do not start lower-priority tasks if higher-priority blockers are open.

Status values:
- Todo
- In Progress
- Blocked
- Done

## Milestone M1: Tooling and Debug Foundation

Target: 40 to 70 hours

| ID | Priority | Task | Estimate (h) | Depends On | Status | Done Criteria |
|---|---|---|---:|---|---|---|
| M1-01 | P1 | Add Windows CI workflow for restore/build/test | 2-4 | None | Done | Workflow runs on push and pull request and is green |
| M1-02 | P1 | Define shared event ID constants and ranges | 4-8 | None | Done | Event ID constants exist and are used in service logs |
| M1-03 | P1 | Add structured logging categories and templates | 4-8 | M1-02 | Done | Service emits categorized logs for lifecycle and connection events |
| M1-04 | P1 | Add correlation IDs for config apply and reconnect flows | 6-10 | M1-03 | Done | Related events include correlation IDs end-to-end |
| M1-05 | P1 | Add rotating file logging sink for local diagnostics | 6-12 | M1-03 | Done | Logs persist locally with rotation and retention settings |
| M1-06 | P1 | Add debug runbook and issue template requirements | 4-6 | M1-03 | Done | Runbook exists and issue template asks for event IDs and logs |

## Milestone M2: Runtime Bootstrap and Security Baseline

Target: 90 to 140 hours

| ID | Priority | Task | Estimate (h) | Depends On | Status | Done Criteria |
|---|---|---|---:|---|---|---|
| M2-01 | P0 | Build full OPC UA ApplicationConfiguration | 12-20 | M1-02 | Done | Config loads and validates at startup with clear errors |
| M2-02 | P2 | Implement certificate store initialization | 10-16 | M2-01 | Done | App creates/loads certs and logs trust state |
| M2-03 | P2 | Implement trust list handling and policy defaults | 12-20 | M2-02 | Done | Trusted/untrusted states are explicit and enforced |
| M2-04 | P0 | Add startup health state model | 8-12 | M2-01 | Done | Service reports healthy/degraded/faulted with reasons |
| M2-05 | P1 | Add security and bootstrap diagnostics to UI contract | 8-14 | M2-03 | Done | UI can query cert and bootstrap status safely |

## Milestone M3: Connection Manager and Local Server Path

Target: 120 to 180 hours

| ID | Priority | Task | Estimate (h) | Depends On | Status | Done Criteria |
|---|---|---|---:|---|---|---|
| M3-01 | P0 | Implement upstream endpoint config model | 10-16 | M2-01 | Done | Endpoint model is persistent and validated |
| M3-02 | P0 | Implement connect/reconnect/backoff lifecycle | 22-36 | M3-01 | Done | Connections recover predictably with observable states |
| M3-03 | P0 | Implement session health and metrics snapshot | 12-20 | M3-02 | Done | Health and counters available for diagnostics |
| M3-04 | P0 | Host local OPC UA server endpoint | 20-30 | M2-01 | Done | Local endpoint is reachable and stable |
| M3-05 | P3 | Implement basic namespace projection | 20-30 | M3-04, M3-02 | Done | Browse/read/subscribe works for mapped nodes |

## Milestone M4: Mapping, UI Operations, and V1 Cut

Target: 160 to 260 hours

| ID | Priority | Task | Estimate (h) | Depends On | Status | Done Criteria |
|---|---|---|---:|---|---|---|
| M4-01 | P3 | Implement mapping model (mirror + rename) | 20-34 | M3-05 | Done | Mapping rules apply correctly and are persisted |
| M4-02 | P4 | Build WinUI shell with Dashboard and Connections | 20-36 | M3-03 | Done | Operator can view health and connection states |
| M4-03 | P4 | Add Logs/Diagnostics view with event filtering | 18-30 | M1-03, M4-02 | Done | Operator can filter by category/severity/event ID |
| M4-04 | P4 | Add safe apply/reload flow in UI | 12-20 | M4-01 | Done | Apply operations validate and show result states |
| M4-05 | P0 | Add core regression tests for lifecycle and mapping | 24-40 | M3-02, M4-01 | Done | Tests cover key reconnect and mapping scenarios |
| M4-06 | P1 | Finalize operator/developer docs for V1 | 8-14 | M4-03, M4-05 | Done | Docs cover setup, operations, and troubleshooting |

## Capacity Planning (example)

Use this to forecast schedule based on weekly available time.

- 10 hours/week: 45 to 70 weeks for 450 to 700 hours
- 20 hours/week: 23 to 35 weeks for 450 to 700 hours
- 30 hours/week: 15 to 24 weeks for 450 to 700 hours

## Second-Pass Implementation Program

The milestones above represent the scaffold/baseline pass. The next phase should convert each area into a reliable, testable implementation that meets the V1 blockers in `V1_SCOPE.md`.

Guiding rules for the second pass:

- Treat existing milestone work as baseline scaffolding unless it has runtime verification, failure-path handling, and regression coverage.
- Use UI work to define operator workflows and structure, but do not move runtime or protocol logic out of Core/Service.
- Prefer thin UI contracts over UI-owned state when the source of truth belongs to the service.
- Every milestone in this phase should have explicit runtime verification steps, log/event expectations, and regression tests where practical.

## Milestone M5: UI Structure and Operator Workflow Pass

Target: 40 to 80 hours

Purpose: lock the application layout, screen structure, and operator task flow early so the remaining service and contract work has a stable target.

| ID | Priority | Task | Estimate (h) | Depends On | Status | Done Criteria |
|---|---|---|---:|---|---|---|
| M5-01 | P4 | Define the real WinUI information architecture | 8-14 | M4-02 | Done | Primary sections, navigation model, and page responsibilities are documented and reflected in the shell |
| M5-02 | P4 | Build the real shell layout and page structure | 12-20 | M5-01 | Done | Window layout, navigation, and page composition match the intended operator workflow |
| M5-03 | P4 | Define UI state model and service-facing contracts | 8-14 | M5-01 | Done | UI state boundaries are documented and transient UI state is separated from service-owned state |
| M5-04 | P4 | Design safe operator flows for config apply/reload and diagnostics | 8-16 | M5-02, M5-03 | Done | Apply, reload, startup status, and failure states have clear UI flows and visible outcomes |
| M5-05 | P1 | Add UI smoke-test checklist for local validation | 4-8 | M5-04 | Done | Repeatable checklist exists for launch, navigation, diagnostics load, and config flow checks |

Progress notes for M5:
- WinUI shell moved to modular controls (`DashboardOverview`, `ConnectionsEditor`, `LogsViewer`, `LiveOutputViewer`) with top-level navigation and bottom status bar.
- Service/UI IPC contract implemented in shared Core (`src/UAGateway.Core/Ipc/IpcContract.cs`) with handshake, startup health, security bootstrap, and connection snapshot methods.
- Service now hosts control and event named-pipe endpoints and UI consumes both channels for status + live output.
- Live Output is event-stream-first and now rendered as append-only terminal-style text.
- M5-04 flow decomposition captured in `docs/M5_04_OPERATOR_FLOW_BLUEPRINT.md` as small execution blocks with acceptance criteria.
- M5-05 checklist published in `docs/UI_SMOKE_CHECKLIST.md` for repeatable 15-minute local validation.
- UI status bar now uses operator-focused service conditions (`Connected`, `Limited`, `Failed`, `Offline`) with deterministic precedence and recoverable action buttons.
- Connections flow now uses Configuration terminology, protects unsaved edits during reload, and prevents overlapping apply/reload operations.
- **In-app Help Center** implemented as a standalone movable window (`HelpWindow.cs`) with searchable TreeView navigation, category grouping, markdown rendering via CommunityToolkit `MarkdownTextBlock`, and working link handling (external URLs open in browser, relative doc links navigate within the window).
- Help content in `docs/HELP.md` is a first-pass placeholder — **must be rewritten with accurate operator content before V1** (tracked as M9-03).

## Milestone M6: Runtime Bootstrap and Security Hardening Pass

Target: 60 to 110 hours

Purpose: turn the startup and certificate baseline into a reliable implementation with deterministic degraded/faulted behavior.

| ID | Priority | Task | Estimate (h) | Depends On | Status | Done Criteria |
|---|---|---|---:|---|---|---|
| M6-01 | P0 | Harden startup configuration validation paths | 10-18 | M2-01, M5-03 | Done | Missing, partial, and invalid config states produce deterministic health results and actionable logs |
| M6-02 | P2 | Harden certificate bootstrap and trust-list workflows | 14-24 | M2-02, M2-03 | Done | Certificate initialization, trust failures, and recovery paths are explicit, logged, and testable |
| M6-03 | P0 | Separate healthy, degraded, and faulted startup semantics end-to-end | 10-18 | M2-04, M6-01 | Done | Service and UI consistently distinguish startup success, degraded operation, and terminal startup failure |
| M6-04 | P1 | Add regression coverage for bootstrap and security failure paths | 12-22 | M6-01, M6-02 | Done | Tests cover invalid config, trust failure, and startup health-state transitions |

## Milestone M7: Connection Lifecycle and Health Hardening Pass

Target: 80 to 140 hours

Purpose: make connection management predictable under failure, reconnect, and multi-endpoint conditions.

| ID | Priority | Task | Estimate (h) | Depends On | Status | Done Criteria |
|---|---|---|---:|---|---|---|
| M7-01 | P0 | Formalize connection state machine and transition rules | 12-20 | M3-02 | Todo | Valid states, transitions, retries, and terminal conditions are explicit in code and docs |
| M7-02 | P0 | Harden reconnect, backoff, and stale-session cleanup behavior | 18-30 | M7-01 | Todo | Reconnect behavior is deterministic and stale sessions/resources are cleaned up safely |
| M7-03 | P0 | Expand endpoint/session health metrics and transition history | 12-20 | M3-03, M7-01 | Todo | Health snapshots capture actionable per-endpoint state and recent transitions |
| M7-04 | P0 | Add regression tests for disconnect storms and partial recovery | 18-30 | M7-02, M7-03 | Todo | Tests cover repeated disconnects, mixed endpoint health, and recovery sequencing |

## Milestone M8: Local Server and Mapping Hardening Pass

Target: 80 to 140 hours

Purpose: make projected namespace behavior stable, observable, and safe across configuration changes.

| ID | Priority | Task | Estimate (h) | Depends On | Status | Done Criteria |
|---|---|---|---:|---|---|---|
| M8-01 | P0 | Harden local server lifecycle and restart behavior | 14-24 | M3-04, M7-02 | Todo | Local endpoint starts, stops, and restarts cleanly with clear diagnostics |
| M8-02 | P3 | Harden mapping application and projection refresh behavior | 16-28 | M4-01, M8-01 | Todo | Mapping changes apply predictably without inconsistent projection state |
| M8-03 | P3 | Verify browse/read/subscribe stability across projected namespaces | 16-28 | M3-05, M8-02 | Todo | Baseline projection behavior is stable under realistic multi-endpoint scenarios |
| M8-04 | P0 | Add regression tests for mapping and projection edge cases | 18-30 | M8-02, M8-03 | Todo | Tests cover mapping conflicts, refresh ordering, and projection stability |

## Milestone M9: Release and Operations Baseline Pass

Target: 40 to 80 hours

Purpose: close the gap between a developer scaffold and a repeatable V1 engineering baseline.

| ID | Priority | Task | Estimate (h) | Depends On | Status | Done Criteria |
|---|---|---|---:|---|---|---|
| M9-01 | P1 | Finalize reproducible local validation workflow | 8-14 | M5-05, M6-04, M7-04, M8-04 | Todo | Local validation checklist covers build, test, service run, UI run, and incident capture expectations |
| M9-02 | P1 | Add intentional publish/distribution workflow for service and UI | 10-16 | M8-01 | Todo | Publish steps are documented and produce known-good outputs for local deployment/testing |
| M9-03 | P1 | Review docs against actual runtime and operator flows | 8-14 | M9-01, M9-02 | Todo | Docs match the implemented runtime, UI behavior, and troubleshooting workflow. **Includes full rewrite of `docs/HELP.md` with accurate operator content for V1.** |
| M9-04 | P0 | Confirm V1 blockers with final hardening checklist | 10-18 | M6-04, M7-04, M8-04, M9-03 | Todo | Each must-have item in `V1_SCOPE.md` has explicit evidence of completion or a tracked blocker |

## Current focus recommendation

Baseline scaffold work is complete; use the second-pass milestones above as the active plan.

Recommended first in-progress task:
- Start M7-01 by formalizing the connection state machine and transition rules.
- Follow immediately with M7-02 reconnect/backoff hardening to lock deterministic recovery behavior.

Current status note (2026-06-04):
- M6 is complete and validated with green build/tests.
- M7 is intentionally deferred for now due session AI budget constraints.
- Next session should resume at M7-01 unless priorities change.

Progress notes for M6:
- Local server startup now reads persisted server settings from `config/server-settings.json` and validates host, port, and endpoint path before binding the OPC UA listener.
- WinUI now exposes a `Server Settings` tab for local listener settings with endpoint preview, restart-required messaging, and reload/apply handling consistent with other configuration editors.
- Port-conflict startup failures now report the configured listener address so operator recovery is explicit.
- Startup fault reasons now map invalid configuration modes to deterministic operator messages (invalid server settings JSON, invalid server settings values, invalid upstream endpoint config, invalid namespace mapping config).
- Added focused store/validation failure-path tests for server settings creation, malformed JSON, null payloads, and invalid persisted port values.
- Startup now reports `Degraded` when security bootstrap finishes with trust warnings (for example no trusted peers), and preserves deterministic `Faulted` behavior for terminal bootstrap failures.
- UI service status evaluation now honors security snapshot severity, surfacing security `Degraded` as `Limited` and security `Faulted` as `Failed`.
- Regression coverage now includes deterministic startup failure reason mapping tests for invalid config and certificate bootstrap failure, plus startup health-state transition tests.
