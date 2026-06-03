# Implementation Tracker

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
| M2-01 | P0 | Build full OPC UA ApplicationConfiguration | 12-20 | M1-02 | Todo | Config loads and validates at startup with clear errors |
| M2-02 | P2 | Implement certificate store initialization | 10-16 | M2-01 | Todo | App creates/loads certs and logs trust state |
| M2-03 | P2 | Implement trust list handling and policy defaults | 12-20 | M2-02 | Todo | Trusted/untrusted states are explicit and enforced |
| M2-04 | P0 | Add startup health state model | 8-12 | M2-01 | Todo | Service reports healthy/degraded/faulted with reasons |
| M2-05 | P1 | Add security and bootstrap diagnostics to UI contract | 8-14 | M2-03 | Todo | UI can query cert and bootstrap status safely |

## Milestone M3: Connection Manager and Local Server Path

Target: 120 to 180 hours

| ID | Priority | Task | Estimate (h) | Depends On | Status | Done Criteria |
|---|---|---|---:|---|---|---|
| M3-01 | P0 | Implement upstream endpoint config model | 10-16 | M2-01 | Todo | Endpoint model is persistent and validated |
| M3-02 | P0 | Implement connect/reconnect/backoff lifecycle | 22-36 | M3-01 | Todo | Connections recover predictably with observable states |
| M3-03 | P0 | Implement session health and metrics snapshot | 12-20 | M3-02 | Todo | Health and counters available for diagnostics |
| M3-04 | P0 | Host local OPC UA server endpoint | 20-30 | M2-01 | Todo | Local endpoint is reachable and stable |
| M3-05 | P3 | Implement basic namespace projection | 20-30 | M3-04, M3-02 | Todo | Browse/read/subscribe works for mapped nodes |

## Milestone M4: Mapping, UI Operations, and V1 Cut

Target: 160 to 260 hours

| ID | Priority | Task | Estimate (h) | Depends On | Status | Done Criteria |
|---|---|---|---:|---|---|---|
| M4-01 | P3 | Implement mapping model (mirror + rename) | 20-34 | M3-05 | Todo | Mapping rules apply correctly and are persisted |
| M4-02 | P4 | Build WinUI shell with Dashboard and Connections | 20-36 | M3-03 | Todo | Operator can view health and connection states |
| M4-03 | P4 | Add Logs/Diagnostics view with event filtering | 18-30 | M1-03, M4-02 | Todo | Operator can filter by category/severity/event ID |
| M4-04 | P4 | Add safe apply/reload flow in UI | 12-20 | M4-01 | Todo | Apply operations validate and show result states |
| M4-05 | P0 | Add core regression tests for lifecycle and mapping | 24-40 | M3-02, M4-01 | Todo | Tests cover key reconnect and mapping scenarios |
| M4-06 | P1 | Finalize operator/developer docs for V1 | 8-14 | M4-03, M4-05 | Todo | Docs cover setup, operations, and troubleshooting |

## Capacity Planning (example)

Use this to forecast schedule based on weekly available time.

- 10 hours/week: 45 to 70 weeks for 450 to 700 hours
- 20 hours/week: 23 to 35 weeks for 450 to 700 hours
- 30 hours/week: 15 to 24 weeks for 450 to 700 hours

## Current focus recommendation

Start with M1-01 through M1-03 before any new feature work.

Recommended first in-progress task:
- M2-01 Build full OPC UA ApplicationConfiguration
