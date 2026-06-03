# Push Checklist

Use this checklist before every push.

## Required

1. Build succeeds.
2. Tests pass for impacted areas.
3. `docs/IMPLEMENTATION_TRACKER.md` reflects actual status.
4. Any changed behavior is documented in the relevant docs.
5. New or changed behavior has test coverage or an explicit note about why not.

## Common Docs To Review

- `docs/IMPLEMENTATION_TRACKER.md`
- `docs/OPERATIONS_AND_DEV_GUIDE.md`
- `docs/TESTING_GUIDELINES.md`
- `docs/SERVICE_INTEGRATION_TESTING_GUIDE.md`
- `docs/ROADMAP.md`
- `README.md`

## Hook Behavior

The repository pre-push hook prompts this checklist before push.

- To enable repo hooks locally: `git config core.hooksPath .githooks`
- To bypass once (rare): `SKIP_PUSH_CHECK=1 git push`
