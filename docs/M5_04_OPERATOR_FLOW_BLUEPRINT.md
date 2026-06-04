# M5-04 Operator Flow Blueprint

Purpose: define safe, predictable operator flows for config apply/reload, startup status handling, and diagnostics outcomes.

This milestone is UI-centered with thin service contract checks, not runtime hardening.

## Scope Boundaries

In scope:

- Operator-facing flow behavior and state transitions in UI
- Visible outcomes for success, validation errors, degraded states, and unavailable service
- Contract-level expectations for existing IPC methods/events

Out of scope:

- New runtime reconnection algorithms
- New certificate policy behavior
- Deep startup-hardening logic (M6)

## Terminology Alignment

Use these terms consistently:

- UI/operator term: `Configuration`
- Service contract term (current): `DraftConfig` (for example `connections.applyDraftConfig`)

Rule:

- UI text, labels, and operator guidance should use `Configuration`.
- Contract/API references may keep `DraftConfig` until contract renaming is scheduled.

## Block 1: Global UX Contract

Goal:

- Define one shared UX language for statuses, errors, and progress.

Deliverables:

- Status taxonomy used across Dashboard, Connections, Logs, Live Output
- Message style guide (operator-readable, actionable, concise)
- Standard loading/disabled behavior during in-flight operations

### Block 1 Agreed Decisions

Canonical status label format:

- Service Status: Connected
- Service Status: Limited
- Service Status: Failed
- Service Status: Offline

Rule for Limited:

- Limited must always include what is limited and why.
- Required format: Service Status: Limited (<limiter summary>)
- Examples:
	- Service Status: Limited (No security snapshot)
	- Service Status: Limited (2/5 servers connected)
	- Service Status: Limited (Startup validation warnings)

Multiple-condition rule:

- When multiple service conditions are active, show the highest-priority condition in the status bar.
- Use fixed priority ordering to avoid status flicker and ambiguous messaging.

Status priority order (highest to lowest):

1. Service Status: Failed
2. Service Status: Offline
3. Service Status: Limited (security/bootstrap)
4. Service Status: Limited (connectivity)
5. Service Status: Limited (diagnostics freshness)
6. Service Status: Connected

### Status Reason Text Catalog (Block 1 standard)

Use short, operator-readable phrases. Keep reason text under ~60 characters where practical.

Failed reasons:

- Service startup failed
- IPC contract mismatch
- Critical service dependency failure

Offline reasons:

- Service unreachable
- IPC control channel unavailable
- Service not running

Limited reasons (security/bootstrap):

- No security snapshot
- Security bootstrap warnings
- Certificate trust issues detected

Limited reasons (connectivity):

- Partial upstream connectivity (<connected>/<enabled> connected)
- Reconnect attempts in progress
- Upstream endpoint failures detected

Limited reasons (diagnostics freshness):

- Diagnostics snapshot stale
- Live event stream disconnected
- Snapshot refresh pending

Connected reason:

- All monitored services nominal

Canonical status line format:

- `Service Status: <State> (<Reason>)`

Examples:

- Service Status: Connected (All monitored services nominal)
- Service Status: Limited (Partial upstream connectivity 2/5 connected)
- Service Status: Failed (Service startup failed)
- Service Status: Offline (IPC control channel unavailable)

### IPC to Status Mapping Matrix (implementation plan)

Evaluation order: top to bottom (first match wins), aligned with fixed priority.

| Priority | Condition (from UI-observed state/IPC) | Status line | Action hint |
|---|---|---|---|
| 1 | Handshake succeeded but startup snapshot is `Faulted` | `Service Status: Failed (Service startup failed)` | `Open Logs` |
| 2 | IPC handshake unavailable or control pipe unreachable | `Service Status: Offline (IPC control channel unavailable)` | `Retry IPC` |
| 3 | Security snapshot unavailable or indicates warnings/fault-adjacent risk while service remains online | `Service Status: Limited (Security bootstrap warnings)` or `Service Status: Limited (No security snapshot)` | `Open Logs` |
| 4 | Connection snapshot available and `ConnectedEndpointCount < EnabledEndpointCount` | `Service Status: Limited (Partial upstream connectivity <connected>/<enabled> connected)` | `Open Connections` |
| 5 | Diagnostics freshness issue (event stream disconnected or snapshot stale threshold exceeded) while service online | `Service Status: Limited (Diagnostics snapshot stale)` or `Service Status: Limited (Live event stream disconnected)` | `Refresh` |
| 6 | Handshake and snapshots available, startup healthy or non-faulted, full connectivity, diagnostics fresh | `Service Status: Connected (All monitored services nominal)` | none |

Freshness thresholds (initial defaults):

- Connection/security snapshot stale threshold: 60 seconds
- Live event stream disconnect grace period before Limited: 15 seconds

Fallback rules:

- If connection snapshot is missing but handshake is healthy, do not mark Connected; prefer Limited with reason `Diagnostics snapshot stale`.
- If multiple Limited conditions are active, show the highest-priority Limited reason by matrix order.
- Keep last known reason for up to 10 seconds during transient refresh to avoid label flicker.

Bottom status bar ownership:

- Bottom status bar is the canonical source of truth for service status.
- Dashboard and other views mirror this state, but do not redefine it.

Recoverable action rule:

- Show action buttons only for recoverable states.
- Examples: Retry IPC, Reload Configuration, Open Logs.

Apply failure rule:

- Keep user edits.
- Show inline summary plus field-level hints.

Service dependency:

- No new service methods required.

Acceptance:

1. Every operator action ends in a visible success/failure/degraded outcome.
2. UI never leaves ambiguous state after command completion or failure.
3. If status is Limited, the limiter reason is always visible without opening logs.
4. If multiple service conditions are active, displayed status follows fixed priority order.
5. Mapping from IPC/snapshots to status line is deterministic and testable.

## Block 2: Startup and Service Availability Flow

Goal:

- Make startup state and UI-service connectivity explicit and understandable.

Deliverables:

- Startup flow matrix for: service offline, handshake failure, startup degraded, startup healthy, snapshot unavailable
- Bottom status bar behavior spec for each state
- Retry strategy for snapshot refresh on reconnect

Service dependency:

- Existing methods: `system.handshake`, `health.getStartup`, `security.getBootstrap`, `connections.getSnapshot`

### Block 2 Startup Matrix (implementation plan)

| Scenario | Detection signal | Status bar line | Primary UI action | Secondary UI behavior |
|---|---|---|---|---|
| Service not running/offline | Handshake timeout or control pipe connect failure | `Service Status: Offline (IPC control channel unavailable)` | `Retry IPC` | Keep latest known snapshots visible but marked stale |
| Handshake protocol mismatch | Handshake response error `ProtocolVersionUnsupported` | `Service Status: Failed (IPC contract mismatch)` | `Open Logs` | Show compatibility warning in Dashboard header |
| Startup faulted | `health.getStartup` returns `Faulted` | `Service Status: Failed (Service startup failed)` | `Open Logs` | Disable apply action by default; allow reload |
| Startup limited | `health.getStartup` returns non-faulted but limited/partial readiness signal | `Service Status: Limited (Startup validation warnings)` | `Open Logs` | Keep apply enabled with warning banner |
| Startup healthy, snapshots pending | Handshake OK but one or more snapshots unavailable | `Service Status: Limited (Snapshot refresh pending)` | `Refresh` | Show per-card loading indicators until snapshots arrive |
| Startup healthy, all required snapshots available | Handshake OK, startup non-faulted, snapshots fresh | `Service Status: Connected (All monitored services nominal)` | none | Normal interactive behavior |

Required startup cards behavior:

- Dashboard startup card must show source and freshness (`via IPC`, updated time).
- If IPC is down and cached file snapshot exists, show stale indicator and keep last known values.
- If no snapshot exists at all, show explicit `Unavailable` placeholder text (not blank).

### Block 2 State Transition Rules

1. Offline -> Connected/Limited only after successful handshake and at least one fresh startup-health response.
2. Connected -> Offline only after consecutive handshake failures for one full retry window (avoid single-sample drops).
3. Failed state has precedence over Offline only when a valid startup fault response is available; otherwise show Offline.
4. During refresh, keep previous status reason for up to 10 seconds to avoid flicker.

### Block 2 Retry Strategy

Automatic retry cadence:

- Handshake retry: every 5 seconds while Offline.
- Snapshot refresh retry (startup/security/connection): every 10 seconds while connected but incomplete.
- Event stream reconnect: existing cadence, but status change to Limited only after 15-second grace (Block 1 rule).

Manual retry actions:

- `Retry IPC` triggers immediate handshake + snapshot refresh sequence.
- `Refresh` triggers immediate snapshot refresh only (no reconnect reset).

Backoff and noise control:

- Cap repeated identical warning banners to once every 30 seconds.
- Log full retry details to Live Output/Logs; keep status bar text concise.

Acceptance:

1. Operator can distinguish Offline vs Limited vs Connected without reading logs.
2. Status transitions are monotonic and not flickery.
3. Recovery actions (`Retry IPC`, `Refresh`) are deterministic and safe to repeat.
4. Startup cards never appear blank; unavailable and stale states are explicitly labeled.

## Block 3: Connection Configuration Reload Flow

Goal:

- Ensure reload from service/disk is safe, predictable, and visible.

Deliverables:

- Reload trigger rules (manual, optional auto after reconnect)
- Unsaved-edit conflict behavior (preserve local edits vs replace with loaded configuration)
- Post-reload selection behavior (which endpoint remains selected)

Service dependency:

- Existing and pending contract alignment for configuration read/reload semantics.

### Block 3 Reload Flow (implementation plan)

Reload sources:

1. Manual reload from operator action.
2. Optional auto-reload after IPC reconnect (only when no unsaved local edits exist).

Unsaved-edit detection:

- Track `HasUnsavedChanges` at view level.
- Compare current editable model against last loaded configuration snapshot.
- If no changes, reload is immediate (silent).

Conflict prompt behavior (when unsaved changes exist):

- Prompt title: `Unsaved configuration changes`
- Prompt body: `Reload will replace current unsaved edits with configuration from service.`
- Actions:
	- `Keep Editing` (default, cancel reload)
	- `Reload and Replace` (discard unsaved edits and load latest)
	- `Save Export Copy` (optional future action; out of current scope)

Post-reload selection rules:

1. If previously selected endpoint still exists, keep it selected.
2. If removed, select first available endpoint.
3. If list is empty, clear editor fields and show `No connections configured` hint.

Status messaging after reload:

- Success: `Configuration reloaded from service.`
- Replaced unsaved edits: `Configuration reloaded. Unsaved local edits were replaced.`
- Cancelled by user: `Reload canceled. Local edits were preserved.`
- Reload failed: `Reload failed. Showing last known configuration.`

Auto-reload guardrails:

- Never auto-reload when `HasUnsavedChanges=true`.
- If auto-reload skipped, show subtle banner: `New configuration available. Reload when ready.`

Error handling:

- On IPC/read error, keep current editable model unchanged.
- Keep selection unchanged.
- Offer recoverable action: `Retry Reload`.

### Block 3 Acceptance Additions

3. Reload behavior is deterministic for all three outcomes: preserve, replace, cancel.
4. Auto-reload never destroys unsaved edits.
5. Selection behavior after reload is stable and predictable.

Acceptance:

1. Reload never silently destroys user edits.
2. Operator gets explicit confirmation of what changed after reload.

## Block 4: Apply Configuration Flow

Goal:

- Define authoritative apply behavior with clear validation and completion outcomes.

Deliverables:

- Apply state machine (idle -> validating -> applying -> success/failure)
- Validation issue rendering pattern (first issue + expandable details)
- Correlation ID display strategy for support and troubleshooting

### Block 4 Apply State Machine (implementation plan)

Primary states:

1. `Idle`: No apply in progress.
2. `Validating`: UI performs local validation and awaits service validation result.
3. `Applying`: Service accepted apply and is processing.
4. `Succeeded`: Apply completed and service acknowledged success.
5. `FailedValidation`: Apply rejected due to validation issues.
6. `FailedRuntime`: Apply failed due to service/runtime error.

State transitions:

- `Idle` -> `Validating`: operator clicks `Apply Configuration`.
- `Validating` -> `FailedValidation`: local or service validation issues returned.
- `Validating` -> `Applying`: validation passes and service accepts apply.
- `Applying` -> `Succeeded`: success response received.
- `Applying` -> `FailedRuntime`: timeout, IPC error, or service failure response.
- Any failure state -> `Idle`: operator edits, retries, or reloads configuration.

Concurrency and button behavior:

- While in `Validating` or `Applying`, disable `Apply Configuration` and `Reload Configuration` to avoid overlapping operations.
- Keep editable fields enabled unless explicit lock is needed for consistency.
- Ignore repeated Apply clicks while non-idle (idempotent guard).

### Block 4 Validation Rendering Rules

Inline summary area must show:

- Severity label (`Validation Failed`, `Apply Failed`, `Apply Succeeded`)
- Human-readable first issue summary
- Issue count when multiple issues are present
- Correlation ID (when available)

Field/row mapping behavior:

- Map validation issues to endpoint row and field where possible.
- Keep existing user edits; do not auto-revert on failure.
- Highlight invalid fields until edited or next successful validation.

Expandable details panel:

- Default collapsed.
- Shows full issue list with `code`, `target`, and `message` when available.

### Block 4 Service Contract Expectations (UI-side)

Expected response cues for apply:

- `applied=true` and optional `correlationId` -> success path.
- `applied=false` with issues -> validation failure path.
- transport error/timeout/unknown method -> runtime failure path.

If correlation ID exists:

- Show it in summary text.
- Persist last apply correlation ID in UI session state for quick support copy.

### Block 4 Outcome Message Templates

Success:

- `Apply succeeded. Correlation: <id>.`

Validation failure:

- `Apply failed validation: <count> issue(s). First: [<target>] <message>.`

Runtime failure:

- `Apply failed: service did not complete request. Try again or check logs.`

Timeout:

- `Apply timed out while waiting for service response. You can retry safely.`

### Block 4 Recovery Actions

For `FailedValidation`:

- Action: `Review issues` (expand details)
- Action: `Reload Configuration` (optional)

For `FailedRuntime`:

- Action: `Retry Apply`
- Action: `Open Logs`
- Action: `Retry IPC` if transport appears unavailable

For `Succeeded`:

- Action: `Refresh` (optional) to fetch latest snapshots

### Block 4 Acceptance Additions

3. Apply flow cannot enter ambiguous state when IPC fails mid-operation.
4. Validation issues are visible inline and mappable to edited content.
5. Correlation ID is surfaced for successful apply and any failure that returns one.

Service dependency:

- Existing and pending contract alignment for `connections.applyDraftConfig` response shape.

Acceptance:

1. Validation failures are actionable and mapped to field/endpoint.
2. Apply success includes explicit evidence (timestamp/correlation/result summary).

## Block 5: Diagnostics Outcome Flow

Goal:

- Make diagnostics views useful during normal and degraded operation.

Deliverables:

- Dashboard fallback behavior when snapshot files exist but IPC is unavailable
- Live Output behavior when event stream disconnects/reconnects
- Logs tab ownership boundaries versus Live Output (no duplicated concerns)

Service dependency:

- Existing event stream and snapshot behavior.

### Block 5 Diagnostics Responsibilities (implementation plan)

View ownership rules:

1. Dashboard = current health snapshots and summarized state.
2. Live Output = real-time event stream (append-only, session-time visibility).
3. Logs = historical persisted log lines with filtering/search.

No-overlap rule:

- Live Output should not be positioned as historical truth.
- Logs should not be positioned as real-time guaranteed stream.
- Dashboard should not duplicate raw event/log content.

### Block 5 Availability Matrix

| Service/IPC condition | Dashboard behavior | Live Output behavior | Logs behavior |
|---|---|---|---|
| IPC connected, snapshots fresh, stream connected | Show current snapshot values with `via IPC` freshness | Show incoming events in real time | Show persisted logs normally |
| IPC connected, snapshots stale | Keep last snapshot values + stale indicator | Continue stream if connected; mark stale reason in status if needed | Unchanged |
| IPC connected, stream disconnected | Dashboard unaffected if snapshots available | Show `Live event stream disconnected` banner and reconnect attempts | Unchanged |
| IPC disconnected, local snapshot files available | Show last known values + `stale/offline` badge | Pause incoming updates, keep existing lines, show offline banner | Continue reading persisted logs |
| IPC disconnected, no local snapshots | Show explicit `Unavailable` placeholders | Show offline banner only | Continue reading persisted logs if present; else explicit no-log state |

### Block 5 Freshness and Labels

Dashboard labels:

- `Source: IPC` when actively refreshed from service.
- `Source: Local snapshot` when using file fallback.
- `Freshness: current` or `Freshness: stale` with updated timestamp.

Live Output labels:

- `Connected` when event stream active.
- `Reconnecting` during reconnect attempts.
- `Disconnected` after grace period exceeded.

Logs labels:

- `Historical logs` badge to distinguish from live events.

### Block 5 Filter and Context Behavior

Logs tab:

- Preserve active filters across refresh unless operator resets.
- Show filtered count and source file path.

Live Output tab:

- Preserve pause/auto-scroll state while reconnecting.
- Do not clear lines automatically on disconnect/reconnect.

Dashboard:

- Preserve card expansion/scroll position during background refresh.

### Block 5 Operator Guidance Messages

Recommended concise banners:

- `Using last known diagnostics while service is offline.`
- `Live event stream disconnected. Reconnecting...`
- `Snapshot data is stale. Refresh to update.`
- `Historical logs available in Logs tab.`

### Block 5 Recovery Actions

- Dashboard: `Refresh`, `Retry IPC`
- Live Output: `Reconnect Stream` (or reuse `Retry IPC` if unified)
- Logs: `Reload Logs`

Action availability:

- Show only actions relevant to current degraded/offline condition.

### Block 5 Acceptance Additions

3. Operators can always identify whether a value is live IPC data or fallback local data.
4. Live Output and Logs are clearly differentiated as live stream vs historical records.
5. Disconnect/reconnect cycles do not silently clear operator context (filters, pause state, visible summaries).

Acceptance:

1. Diagnostics remain useful when service is degraded or briefly unavailable.
2. Operator can tell which view is historical logs vs live events.

## Block 6: Error Recovery and Retry UX

Goal:

- Standardize user-initiated recovery actions after failures.

Deliverables:

- Retry affordances for handshake/snapshots/apply/reload
- Backoff and cooldown messaging (UI side)
- Clear "next best action" hints for each top-level failure state

Service dependency:

- No required runtime changes; only consume current results consistently.

### Block 6 Recovery Model (implementation plan)

Principle:

- Every non-success top-level state must provide at least one clear, safe recovery action.

Top-level state to action mapping:

| Top-level status | Likely cause family | Primary action | Secondary action |
|---|---|---|---|
| `Service Status: Offline` | IPC/service unreachable | `Retry IPC` | `Open Logs` |
| `Service Status: Failed` | startup/contract/runtime fault | `Open Logs` | `Retry IPC` |
| `Service Status: Limited (security/bootstrap)` | missing snapshot or trust warnings | `Open Logs` | `Refresh` |
| `Service Status: Limited (connectivity)` | partial upstream connectivity | `Open Connections` | `Refresh` |
| `Service Status: Limited (diagnostics freshness)` | stale snapshots or stream disconnect | `Refresh` | `Retry IPC` |

### Block 6 Retry Semantics

Action semantics:

- `Retry IPC`: immediate handshake + snapshot refresh sequence.
- `Refresh`: snapshot reload only; does not reset reconnect timers.
- `Retry Apply`: reattempt last apply operation using current editable configuration.
- `Reload Configuration`: replace local editable state with latest loaded source.

Safety rules:

- Retry actions must be idempotent from operator perspective.
- Disallow duplicate in-flight retries for the same action key.
- Keep existing user edits unless operator explicitly chooses replace/reload.

### Block 6 Cooldown and Backoff UX

UI cooldown defaults:

- `Retry IPC` button cooldown: 2 seconds after click.
- `Refresh` button cooldown: 1 second after click.
- `Retry Apply` cooldown: until previous apply attempt resolves.

Escalation messaging:

- After 3 consecutive failed `Retry IPC` attempts: show `Service still unavailable. Check service process and logs.`
- After repeated stale snapshot refresh failures: show `Diagnostics data still stale after retries.`

Noise control:

- Do not stack duplicate toast/banner messages; update existing banner text and timestamp.

### Block 6 Next-Best-Action Text

Banner/footer helper text examples:

- Offline: `Unable to reach service. Retry IPC or check service status.`
- Failed: `Service reported a failure. Open logs for details before retrying.`
- Limited connectivity: `Some upstream servers are unavailable. Review Connections.`
- Limited diagnostics: `Data may be stale. Refresh diagnostics.`

### Block 6 Disabled-State Rules

- Disable action buttons only when the same action is already in progress.
- Keep unrelated actions available when safe (for example `Open Logs` always available).
- Show inline reason for disabled state (for example `Retry in progress...`).

### Block 6 Acceptance Additions

3. Each top-level non-success state has an explicit next-best action shown in UI.
4. Retry controls prevent accidental spamming without blocking legitimate recovery attempts.
5. Retry and refresh actions are safe to invoke repeatedly and leave UI state consistent.

Acceptance:

1. Every failure state has at least one obvious recovery action.
2. Recovery actions are idempotent and safe to repeat.

## Block 7: Smoke Validation Plan (Feeds M5-05)

Goal:

- Convert M5-04 decisions into a repeatable local verification list.

Deliverables:

- Scenario grid for happy path + degraded path + offline path
- Expected UI outcomes for each scenario
- Quick manual checklist seeds for M5-05

Acceptance:

1. Each M5-04 flow has at least one explicit verification scenario.
2. Checklist can be executed in under 15 minutes for a quick confidence pass.

### Block 7 Smoke Checklist (15-minute target)

Preconditions:

- UI and service are both runnable locally.
- At least one connection endpoint exists in configuration.
- Logs directory is writable.

Pass criteria notation:

- `PASS`: expected behavior observed exactly.
- `WARN`: behavior usable but wording/visual polish mismatch.
- `FAIL`: behavior missing, ambiguous, or unsafe.

#### Scenario A: Healthy startup baseline (2-3 min)

Steps:

1. Start service and UI.
2. Wait for initial handshake and snapshot load.

Expected:

- Status bar shows `Service Status: Connected (...)` or valid Limited reason if environment is partially connected.
- Dashboard cards show source and timestamp.
- Live Output receives events or clearly shows reconnecting state.

#### Scenario B: Offline recovery path (2-3 min)

Steps:

1. Stop service while UI remains open.
2. Observe status transition.
3. Use `Retry IPC` after restarting service.

Expected:

- Status transitions to `Service Status: Offline (...)` with no ambiguous text.
- Cached diagnostics remain visible as stale where available.
- `Retry IPC` returns UI to Connected/Limited without app restart.

#### Scenario C: Configuration apply validation failure (2-3 min)

Steps:

1. Edit configuration with intentional invalid value.
2. Trigger `Apply Configuration`.

Expected:

- Inline validation summary appears with issue count and first issue.
- Invalid field/row is highlighted.
- Unsaved edits are preserved.

#### Scenario D: Configuration reload conflict handling (2 min)

Steps:

1. Make unsaved local edits.
2. Trigger `Reload Configuration`.
3. Choose cancel path first, then replace path.

Expected:

- Conflict prompt appears.
- `Keep Editing` preserves local edits.
- `Reload and Replace` loads latest configuration and updates status message.

#### Scenario E: Diagnostics ownership clarity (2 min)

Steps:

1. Open Dashboard, Live Output, and Logs in sequence.
2. Trigger/observe new live events.

Expected:

- Dashboard shows summarized health, not raw event feed.
- Live Output shows live append-only stream behavior.
- Logs tab remains historical/filterable and clearly labeled.

#### Scenario F: Retry/cooldown behavior (1-2 min)

Steps:

1. Trigger repeated `Retry IPC` or `Refresh` quickly.

Expected:

- Duplicate in-flight retries are suppressed.
- Controls re-enable after cooldown/response.
- No duplicate stacked warning banners.

### Block 7 Result Summary Template

Record after each smoke pass:

- Date/time:
- Build/test baseline used:
- Scenario results: A/B/C/D/E/F = PASS/WARN/FAIL
- Notes for WARN/FAIL:
- Follow-up issue IDs (if created):

### Block 7 Exit Rule

- M5-04 can be considered behavior-complete when all six scenarios are PASS or documented WARN with approved follow-up items.

Checklist companion:

- `docs/UI_SMOKE_CHECKLIST.md`

## Open Questions To Resolve During M5-04

1. Should reload always prompt when local configuration differs from last loaded state?
2. Should apply be disabled while startup health is faulted, or allowed with warning?
3. What minimum diagnostics are shown when IPC is down but file snapshots exist?
4. Where should correlation IDs be shown by default in UI?

## Suggested Execution Order

1. Block 1
2. Block 2
3. Block 4
4. Block 3
5. Block 5
6. Block 6
7. Block 7

## Definition of Done for M5-04

- Apply, reload, startup status, and diagnostics outcomes are fully specified with UI state transitions.
- Each flow has clear success/failure/degraded handling and visible operator guidance.
- Contract gaps discovered are captured as explicit follow-up items (M6/M7 as appropriate).
- M5-05 smoke checklist can be produced directly from this blueprint.
