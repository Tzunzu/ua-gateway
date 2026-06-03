# V1 Scope Contract

## Planning assumptions

- Primary development is one developer with AI pairing support.
- Target platform for runtime and first UI is Windows.
- V1 prioritizes reliability and diagnostics over feature breadth.

## Must have (V1 blockers)

1. Service runtime foundation
- Stable startup and shutdown
- Structured logging and health state model
- Windows Service hosting and local run mode

2. OPC UA upstream connectivity
- Multiple endpoint configuration support
- Connect/reconnect lifecycle handling
- Session and endpoint health reporting

3. Local OPC UA server exposure
- Local endpoint hosting
- Baseline namespace projection from configured mappings
- Stable browse/read/subscribe behavior

4. Configuration and mapping
- Persistent configuration model
- Mirror and rename mapping rules
- Safe apply/reload flow with validation

5. Security baseline
- Certificate store initialization
- Trust list workflow
- Secure endpoint defaults

6. Operations UI baseline (WinUI 3)
- Dashboard view
- Connections view
- Logs and diagnostics view
- Basic settings for local operation

7. Test and release baseline
- Automated build and test checks
- Core connection/mapping regression tests
- Operator/developer setup docs

## Should have (if schedule allows)

- Write-through safeguards for controlled commands
- Mapping template import/export
- Better alarm/event operator workflows
- Performance tuning for larger tag sets

## Explicitly not in V1

- Linux frontend implementation
- Cloud control plane features
- High-availability clustering
- Full enterprise RBAC model

## Time estimate

- Lean V1: 220 to 320 hours
- Practical industrial V1: 450 to 700 hours
- Hardened V1.0: 700 to 1000+ hours

For planning this project, use 450 to 700 hours as the baseline target.
