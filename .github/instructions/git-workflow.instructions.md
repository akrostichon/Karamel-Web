---
description: 'Universal git workflow rules for all development work in Karamel-Web'
applyTo: '**'
---

# Git Workflow Rules

These rules apply to **all development work** — plan implementation, refactoring, feature development, and bug fixes alike.

## Branch Protection

- **NEVER** commit directly to `main`, `develop`, or `master`
- Always check the current branch first (`git branch`)
- If you are on `main`, `develop`, or `master`: create a feature branch with a user-facing name that describes the goal
  - Pattern: `feature/descriptive-name-of-implementation-goal`
  - Example: `feature/add-session-cleanup`, `feature/sanitize-library-upload`
- If already on a feature branch: stay on it — do not create a new branch

## Commit Policy

- **User-facing commit messages** — describe the outcome, not the internal task
  - ✅ `Add Azure deployment pipeline`
  - ✅ `Fix session cleanup after TTL expiry`
  - ❌ `Implement Phase 3` (internal reference)
  - ❌ `Implement task 2.1` (internal reference)
- Commit **locally by default** — only push when explicitly requested by the user or required by a CI/CD context
- Do not include `plan*.md` or `plan-*.md` files in any commit — they are git-ignored working notes

## Plan File Exclusion

- `plan*.md` and `plan-*.md` files are intentionally git-ignored and must never be staged or committed
- If accidentally staged, unstage before committing: `git restore --staged plan*.md`
