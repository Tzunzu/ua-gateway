# Priority Model

## Priority order

### P0: Reliability and safety

- Connection lifecycle correctness
- Deterministic behavior during disconnect/reconnect
- Clear failure states and recovery paths

### P1: Tooling and debug foundation

- Local developer loop for restore/build/test/run
- Structured logging with stable event IDs
- Reproducible diagnostics and troubleshooting workflow

### P2: Security and trust model

- Certificate and trust handling
- Secure endpoint defaults
- Clear operator-facing security status

### P3: Core gateway behavior

- Mapping correctness
- Namespace consistency
- Controlled write-through behavior

### P4: Operations UX

- Fast health visibility
- Clear connection status and error context
- Safe configuration apply flow

### P5: Performance and polish

- Tag-scale performance improvements
- UI refinements
- Non-critical quality enhancements

## Sequencing rule

Work on higher priorities first. Lower-priority tasks should not block progress on higher-priority milestones.

## Weekly execution guidance

- Reserve at least 20 to 30 percent of weekly time for reliability, tooling, and diagnostics work.
- Do not defer observability until late milestones.
- Treat bug triage and incident reproducibility as first-class engineering work.
