# Tooling, Debug, and Event Plan

## Goal

Start with a solid engineering loop and observable runtime behavior from the beginning.

## Tooling baseline (immediate)

1. Build/test/run tasks in VS Code
- Restore solution
- Build solution
- Test solution
- Run service
- Run UI

2. Local quality controls
- .editorconfig for consistent formatting
- .gitattributes and .gitignore for stable repository behavior

3. CI baseline (recommended next)
- GitHub workflow on push and pull request
- Run restore, build, test on Windows runner

## Logging and event model

Use structured logs with stable event IDs and categories. Every important transition should emit an event.

### Initial categories

- Service lifecycle
- Connection lifecycle
- Security/certificate
- Mapping/configuration
- Server endpoint behavior

### Initial severity guidance

- Information: expected state transitions
- Warning: recoverable issues and retries
- Error: failed operation requiring attention
- Critical: data path unavailable or unsafe state

## Initial event IDs (starting template)

- 1000 to 1099: Service lifecycle
- 2000 to 2199: Upstream connection lifecycle
- 3000 to 3199: Certificate and trust workflow
- 4000 to 4299: Mapping/configuration apply lifecycle
- 5000 to 5299: Local server endpoint lifecycle

## Debug workflow

1. Reproduce issue with deterministic steps
2. Capture relevant event IDs and timestamps
3. Correlate connection, security, and mapping events
4. Confirm fix with targeted test and runtime verification
5. Document root cause and prevention note

## Near-term implementation checklist

1. Introduce shared event ID constants in service/core
2. Add correlation ID support for configuration apply operations
3. Add health snapshot endpoint or service query for UI diagnostics
4. Add rotating file logs for local troubleshooting
5. Add issue template section for required event IDs and logs
