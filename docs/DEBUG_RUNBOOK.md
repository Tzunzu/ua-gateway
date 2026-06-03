# Debug Runbook

## Purpose

Use this runbook to create reproducible diagnostics for UA Gateway service issues.

## Prerequisites

- Build succeeds locally.
- Service can run from VS Code task `Run UAGateway Service`.
- Logging output is available in `src/UAGateway.Service/bin/<Configuration>/net8.0/logs/` when running locally.

## Capture Checklist

Collect these items for every issue:

1. Exact reproduction steps.
2. UTC timestamps for start, failure, and recovery checks.
3. Event IDs observed during the incident.
4. Correlation IDs for related config-apply and reconnect flows.
5. Log excerpts covering at least 30 seconds before and after the failure.
6. Environment details (OS version, branch, commit SHA, local or service-hosted run).

## Reproduction Workflow

1. Start from a clean build:
   - `dotnet restore UAGateway.sln`
   - `dotnet build UAGateway.sln`
2. Run the service.
3. Execute deterministic reproduction steps.
4. Record timestamps as each key action is performed.
5. Collect logs and extract relevant event IDs and correlation IDs.

## Event ID Ranges

- 1000 to 1099: Service lifecycle
- 2000 to 2199: Upstream connection lifecycle
- 3000 to 3199: Certificate and trust workflow
- 4000 to 4299: Mapping and configuration apply lifecycle
- 5000 to 5299: Local server endpoint lifecycle

## Triage Pattern

1. Find the first warning or error event in the time window.
2. Correlate prior lifecycle events by correlation ID.
3. Verify expected start and completion events exist for the same correlation ID.
4. Confirm whether the issue is recoverable or terminal.

## Verification After Fix

1. Re-run the same deterministic reproduction steps.
2. Confirm old failure event sequence no longer appears.
3. Confirm expected completion events appear with matching correlation IDs.
4. Run `dotnet test UAGateway.sln`.
