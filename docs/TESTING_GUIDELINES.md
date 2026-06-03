# Testing Guidelines

## Purpose

Keep tests predictable, fast, and easy to maintain as the gateway evolves.

## Scope and Project Layout

Current test projects:

- `tests/UAGateway.Core.Tests`: unit and serialization/contract tests for `UAGateway.Core`
- `tests/UAGateway.Service.IntegrationTests`: service IPC integration coverage

Near-term direction:

- Keep UI tests separate from Core/Service tests.

For service-level integration patterns, use:

- [Service integration testing guide](SERVICE_INTEGRATION_TESTING_GUIDE.md)

## Core Principles

- Test behavior, not implementation details.
- Prefer small deterministic tests over broad fragile tests.
- Avoid real network, filesystem, clock, and process dependencies unless explicitly writing an integration test.
- One reason to fail per test whenever practical.

## Naming and Structure

Use this pattern for test method names:

- `MethodOrComponent_Condition_ExpectedBehavior`

Examples:

- `UpstreamEndpointValidator_FlagsInvalidScheme`
- `MethodNames_AreUnique`

Use Arrange/Act/Assert structure:

1. Arrange input data and collaborators.
2. Act once.
3. Assert only outcomes that matter for the scenario.

## Assertion Guidelines

- Assert on stable fields and messages/flags that define contract behavior.
- For collections, assert both existence and intent (for example endpoint ID + key message fragment).
- Use case-insensitive checks only where behavior is intentionally case-insensitive.

## Determinism Rules

- Do not depend on wall-clock timing in assertions.
- Do not depend on machine-specific paths or environment state.
- Keep random data constrained and avoid snapshot-style brittle output assertions.

## Coverage Priorities for This Repository

Prioritize tests for these areas first:

1. Configuration validation edge cases (uniqueness, unknown references, required fields).
2. IPC contract stability (method/event constants and JSON shape).
3. Connection-state and startup-health semantics when logic resides in testable units.
4. Mapping behavior (mirror vs rename and conflict handling).

## What Not To Do

- Do not test framework behavior (JSON serializer internals, LINQ internals, etc.) unless it is part of our explicit contract.
- Do not over-assert on log message wording unless the wording itself is a supported operator-facing contract.
- Do not add long-running tests to the default local test path.

## Local Workflow

Use the built-in task:

- `Test UAGateway`
- `Test UAGateway Service Integration`

Equivalent CLI:

```powershell
dotnet test UAGateway.sln
```

Recommended before pushing:

1. Run build.
2. Run tests.
3. Add tests for any bugfix or contract change in the same PR.

## PR Checklist for Tests

- New behavior includes test coverage.
- Existing tests updated only when behavior intentionally changed.
- Test names describe scenario and expected outcome.
- Tests pass locally.
- No flaky timing or machine-dependent assertions introduced.
