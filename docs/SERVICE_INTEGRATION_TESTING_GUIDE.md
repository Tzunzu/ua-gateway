# Service Integration Testing Guide

## Purpose

Capture the pattern for service-level integration tests so we apply it consistently as coverage grows.

## When To Use Integration Tests

Use integration tests when validating behavior across boundaries, for example:

- named pipe IPC request/reply between UI-style client and service host
- event stream delivery and reconnect behavior
- hosted service startup and shutdown sequencing
- connection lifecycle state transitions that require real orchestration

Prefer unit tests for pure validation and data-shape logic.

## Test Scope Rules

- Keep integration tests focused on one end-to-end behavior at a time.
- Run against local in-memory or temporary resources where possible.
- Avoid external network dependencies and machine-specific setup.
- Make tests deterministic: fixed timeouts, bounded retries, explicit cleanup.

## Recommended Project Layout

Current project:

- `tests/UAGateway.Service.IntegrationTests`

Current folders:

- `Host/` for test host builder and dependency overrides
- `Ipc/` for control/event pipe integration tests
- `Lifecycle/` for startup/health/reconnect scenarios
- `Fixtures/` for reusable setup/teardown helpers

## Naming Convention

Use:

- `ComponentOrFlow_Condition_ExpectedOutcome`

Examples:

- `ControlPipe_HandshakeRequest_ReturnsSupportedProtocol`
- `EventStream_WhenClientReconnects_ReceivesSubsequentEvents`
- `Startup_WhenCertificateBootstrapDegraded_ReportsDegradedHealth`

## Minimum Infrastructure Pattern

Each integration test should:

1. Arrange
2. Start host
3. Execute scenario
4. Assert
5. Stop host and cleanup

Arrange:

- create isolated temp directory for config/log/diagnostic outputs
- build host with test-specific options and deterministic timing
- seed configuration documents required by the scenario

Start host:

- start hosted services explicitly
- wait until startup health is available before acting

Execute scenario:

- call control pipe methods using shared IPC contract models
- subscribe to event stream and record received envelopes
- trigger lifecycle action (for example reconnect request)

Assert:

- verify response/result fields, not internal implementation details
- assert expected event category/name/severity and key payload values
- assert startup health state and reason fields when relevant

Cleanup:

- stop host
- dispose clients/subscriptions
- delete temp artifacts created by the test

## Timeout and Retry Guidance

- Keep per-assert wait windows short and explicit.
- Use polling helpers with deterministic max duration.
- Fail with actionable context (expected vs observed events/states).

Default starting points:

- request/response timeout: 2s
- event wait timeout: 5s
- poll interval: 50ms

Adjust only when scenario complexity requires it.

## Assertions We Care About Most

Priority assertions for this repository:

1. IPC contract compatibility (method names, payload shape, response codes)
2. startup health semantics (healthy/degraded/faulted with reasons)
3. connection lifecycle observability (state changes + metrics snapshot updates)
4. event stream reliability (delivery after subscribe/reconnect)

## What Not To Assert

- exact ordering of unrelated background log entries
- machine-specific absolute paths
- fragile timing assumptions based on thread scheduling

## Local Run Guidance

Use the dedicated VS Code task and keep it separate from fast unit tests:

- `Test UAGateway Service Integration`

Suggested CLI shape:

```powershell
dotnet test tests/UAGateway.Service.IntegrationTests/UAGateway.Service.IntegrationTests.csproj
```

## PR Checklist For Integration Tests

- covers a real cross-boundary behavior
- deterministic setup and cleanup included
- clear timeout policy used
- failures provide actionable context
- no external environment dependency introduced

## Bootstrap Checklist For Future Setup

When creating the project, do these first:

1. add project reference to `src/UAGateway.Service`
2. reference `Microsoft.NET.Test.Sdk` and `xunit`
3. create reusable host fixture with temp-path isolation
4. add one handshake IPC test as golden path
5. add one startup-health degraded-path test
