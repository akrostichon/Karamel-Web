---
name: git-workflow
description: Git workflow rules for Karamel-Web. Use when committing code, creating branches, preparing a commit message, checking whether to push, or when asked about branch strategy, git conventions, plan file handling, or how to start/finish a piece of work in git.
---

# Git Workflow

Enforces the Karamel-Web git workflow: branch protection, user-facing commit messages, local-first commits, and plan file exclusion.

**Core principle:** Never commit to protected branches. Describe outcomes in commits, not internal tasks.

## Step 1 — Verify the Current Branch

```powershell
git branch
```

- If on `main`, `develop`, or `master` → go to **Step 2**
- If already on a `feature/…` branch → skip to **Step 3**

## Step 2 — Create a Feature Branch

```powershell
git checkout -b feature/descriptive-name-of-goal
```

**Naming pattern:** `feature/<what-this-achieves>` — user-facing, goal-oriented.

| ✅ Good | ❌ Bad |
|---|---|
| `feature/add-session-cleanup` | `feature/phase-3` |
| `feature/sanitize-library-upload` | `feature/task-2.1` |
| `feature/fix-ttl-expiry-on-heartbeat` | `feature/implement-plan` |

Do **not** create a new branch if already on a feature branch — stay on it.

## Step 3 — Stage and Review Files

```powershell
git status
git diff --staged
```

**Remove plan files if accidentally staged:**

```powershell
git restore --staged plan*.md
git restore --staged "plan-*.md"
```

`plan*.md` and `plan-*.md` files are git-ignored working notes. They must **never** appear in any commit.

## Step 4 — Commit with a User-Facing Message

```powershell
git commit -m "Describe the outcome, not the task"
```

**Commit message rules:**

| ✅ Good | ❌ Bad |
|---|---|
| `Add Azure deployment pipeline` | `Implement Phase 3` |
| `Fix session cleanup after TTL expiry` | `Implement task 2.1` |
| `Show error when session validation fails` | `WIP` |

- Describe **what changed for the user**, not what internal step was done
- Be specific enough that the message is meaningful in `git log`
- No issue numbers required, but acceptable when directly relevant

## Step 5 — Push Only When Requested

- **Do NOT push by default** — commit locally
- Push only when the user explicitly asks, or when a CI/CD context requires it

```powershell
# Only when explicitly requested:
git push -u origin feature/your-branch-name
```

## Quick Reference

| Situation | Action |
|---|---|
| On `main`/`develop`/`master` | Create `feature/…` branch first |
| Already on feature branch | Stay on it — no new branch |
| plan*.md accidentally staged | `git restore --staged plan*.md` |
| Ready to commit | Commit locally — do NOT auto-push |
| User asks to push | Push the current feature branch |

## Red Flags

**Never:**
- Commit directly to `main`, `develop`, or `master`
- Include `plan*.md` or `plan-*.md` in any commit
- Auto-push without explicit user request
- Use internal task names (`Phase 3`, `task 2.1`) in commit messages
- Create a second feature branch when already on one

**Always:**
- Check `git branch` before starting any commit work
- Use `feature/` prefix for branch names
- Write commit messages that describe user-visible outcomes

## Common Mistakes

**Committing to main**
- **Problem:** Breaks branch protection, no PR review possible
- **Fix:** If you've already committed, `git reset HEAD~1 --soft`, create a feature branch, re-commit

**Plan files in commits**
- **Problem:** Working notes pollute the repository history
- **Fix:** `git restore --staged plan*.md` before committing; they are gitignored so `git add .` should not pick them up, but verify

**Internal commit messages**
- **Problem:** `git log` becomes useless, unreadable to other contributors
- **Fix:** Describe what the user gets, not what code was touched

**Pushing without being asked**
- **Problem:** Premature pushes in PR-based workflows disrupt review process
- **Fix:** Always commit locally by default; only push when explicitly requested

## Integration

**Called when:** Committing code, asking about branching, preparing a pull request, checking workflow rules.

**Pairs with:**
- `code-review` skill — for review before merging
- `refactoring-workflow` skill — always operates on a feature branch
- `tdd-workflow` / `with-tests-workflow` — commit after tests pass
