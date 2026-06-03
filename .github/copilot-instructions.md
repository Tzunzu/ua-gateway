# Copilot Repository Instructions

Apply these steps before every commit and push.

## Required Before Push

1. Run build and tests for impacted scope.
2. Update `docs/IMPLEMENTATION_TRACKER.md` when milestone status, scope, or current focus changes.
3. Update related docs when behavior changes (for example operations, testing, architecture, roadmap, or README continuity links).
4. Add or update tests for new behavior, or explicitly note why tests were not added.
5. Review `docs/PUSH_CHECKLIST.md` before pushing.

## Push Safety

- Repository pre-push hook enforces a checklist reminder.
- For non-interactive automation, push will be blocked unless explicitly bypassed.
- One-time bypass: `SKIP_PUSH_CHECK=1 git push`.

## Commit Hygiene

- Keep commit messages clear and scoped.
- Do not mix unrelated changes in one commit when avoidable.
- Prefer small follow-up commits for docs/checklist corrections.
