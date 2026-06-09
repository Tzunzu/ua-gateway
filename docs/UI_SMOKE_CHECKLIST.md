# UI Smoke Checklist

Purpose: quick, repeatable local validation for UI operator flows.

Target duration: about 15 minutes.

## Preconditions

- Service and UI can both run locally.
- At least one endpoint exists in configuration.
- Logs directory is writable.

## Result Labels

- PASS: expected behavior observed.
- WARN: usable behavior with wording/polish mismatch.
- FAIL: behavior missing, ambiguous, or unsafe.

## Scenario A: Healthy Startup Baseline

Steps:

1. Start service and UI.
2. Wait for handshake and initial snapshot load.

Expected:

- Status bar shows `Service Status: Connected (...)` or a valid Limited reason.
- Dashboard cards show source and updated timestamp.
- Live Output receives events or clearly shows reconnecting state.

## Scenario B: Offline Recovery

Steps:

1. Stop service while UI remains open.
2. Observe status transition.
3. Restart service and use Retry IPC.

Expected:

- Status transitions to `Service Status: Offline (...)` with clear reason.
- Cached diagnostics stay visible and marked stale where available.
- Retry IPC returns to Connected/Limited without restarting UI.

## Scenario C: Apply Validation Failure

Steps:

1. Enter intentionally invalid configuration.
2. Trigger Apply Configuration.

Expected:

- Inline validation summary appears with count and first issue.
- Invalid field/row is highlighted.
- Local edits are preserved.

## Scenario D: Reload Conflict Handling

Steps:

1. Make unsaved local edits.
2. Trigger Reload Configuration.
3. Choose Keep Editing, then retry and choose Reload and Replace.

Expected:

- Conflict prompt appears.
- Keep Editing preserves local edits.
- Reload and Replace loads latest configuration and shows explicit status message.

## Scenario E: Diagnostics Ownership Clarity

Steps:

1. Navigate via left `NavigationView` items: Dashboard, Live Output, and Logs in sequence.
2. Trigger or observe live events.

Expected:

- Dashboard shows summarized health, not raw event stream.
- Live Output shows append-only live stream behavior.
- Logs remains historical and filterable.

## Scenario F: Retry and Cooldown Behavior

Steps:

1. Rapidly trigger Retry IPC or Refresh.

Expected:

- Duplicate in-flight retries are suppressed.
- Controls re-enable after cooldown/response.
- Duplicate warning banners are not stacked.

## Run Record Template

- Date/time:
- Build/test baseline used:
- Scenario results: A/B/C/D/E/F = PASS/WARN/FAIL
- Notes for WARN/FAIL:
- Follow-up issue IDs:

## Latest Run Record

- Date/time: 2026-06-09
- Build/test baseline used: `dotnet build UAGateway.sln` and `dotnet test UAGateway.sln` both green
- Scenario results: A/B/C/D/E/F = Pending manual desktop validation
- Notes for WARN/FAIL: Service and UI launch paths were verified after shell migration; the WinUI process needed a debugger-side desktop interaction loop to complete scenario execution, which is not available in this tool session. The startup crash in `SettingsPage` was fixed and revalidated.
- Follow-up issue IDs: None yet

## Exit Rule

- Checklist pass is complete when all scenarios are PASS, or WARN items have approved follow-up tasks.
