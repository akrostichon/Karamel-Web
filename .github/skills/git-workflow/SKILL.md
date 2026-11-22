---
name: git-workflow
description: Git workflow guidance for Karamel-Web. Use when committing code, creating branches, preparing for Matt Pocock's code-review, implement, or implement-spec workflows, checking whether to push, or when asked about branch strategy, plan-file handling, or Git conventions.
---

# Git Workflow

Guides the Karamel-Web Git workflow: work branches, user-facing commit messages, local-first commits, and plan-file exclusion. These instructions guide agent behavior; use VS Code prompts for convenience and GitHub rulesets for server-side enforcement.

**Core principle:** Never commit to protected branches. Describe outcomes in commits, not internal tasks.

## Step 1 — Verify the Current Branch

```powershell
git branch
```

- If on `main`, `develop`, or `master` → go to **Step 2**
- If already on a `feature/…` branch → skip to **Step 3**

## Step 2 — Create a Work Branch

```powershell
git checkout -b feature/descriptive-name-of-goal
```

For ordinary work, create a `feature/…` branch. When an explicitly invoked workflow requires another branch type, use its branch name instead; for example, Matt Pocock's `implement-spec` requires an integration branch.

**Naming pattern:** `feature/<what-this-achieves>` — user-facing, goal-oriented.

| ✅ Good | ❌ Bad |
|---|---|
| `feature/add-session-cleanup` | `feature/phase-3` |
| `feature/sanitize-library-upload` | `feature/task-2.1` |
| `feature/fix-ttl-expiry-on-heartbeat` | `feature/implement-plan` |

For ordinary work, keep using the current feature branch. If a higher-level workflow explicitly requires a dedicated branch, follow that requirement instead. In particular, Matt Pocock's `implement-spec` requires an integration branch; create or switch to that branch even when starting from another feature branch. Never use `main`, `develop`, or `master` as the work branch.

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
| `Add Render deployment pipeline` | `Implement Phase 3` |
| `Fix session cleanup after TTL expiry` | `Implement task 2.1` |
| `Show error when session validation fails` | `WIP` |

- Describe **what changed for the user**, not what internal step was done
- Be specific enough that the message is meaningful in `git log`
- No issue numbers required, but acceptable when directly relevant

## Step 5 — Review and Publish

- Matt Pocock's `code-review` compares commits from a fixed point to `HEAD`; it does not include uncommitted changes. Commit the work locally before invoking that review. If the `implement` workflow calls for review before its final commit, commit the implementation first, review it, then commit any fixes separately.
- Keep commits local by default. Push only when the user asks or an explicitly invoked workflow requires a remote action. For example, `implement-spec` calls for a draft PR when the issue-tracker workflow closes work through PRs; when that condition applies, push the integration branch as needed to create or update the PR.
- A review agent that requires a pushed branch does not itself authorize a push. Ask the user unless a workflow or request already authorizes publishing.

```powershell
# Only when requested or required by the active workflow:
git push -u origin feature/your-branch-name
```

## Quick Reference

| Situation | Action |
|---|---|
| On `main`/`develop`/`master` | Create the work branch required by the active workflow; use `feature/…` for ordinary work |
| Already on a feature branch | Keep it for ordinary work; follow an explicitly required workflow branch (such as `implement-spec`'s integration branch) |
| plan*.md accidentally staged | `git restore --staged plan*.md` |
| Ready to review with Matt's `code-review` | Commit locally first so the diff includes the work |
| Ready to publish | Push only when requested or required by the active workflow |

## Red Flags

**Never:**
- Commit directly to `main`, `develop`, or `master`
- Include `plan*.md` or `plan-*.md` in any commit
- Push without an explicit user request or a remote action required by the active workflow
- Use internal task names (`Phase 3`, `task 2.1`) in commit messages
- Replace a workflow-required integration branch with the current feature branch

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

**Reviewing before committing**
- **Problem:** Matt's `code-review` only sees commits up to `HEAD`, so uncommitted changes are absent from its diff.
- **Fix:** Commit locally before review, then commit any review fixes separately.

**Pushing without authorization**
- **Problem:** Publishing a branch or PR can trigger external automation and expose work remotely.
- **Fix:** Push only after an explicit user request or when the active workflow's stated condition requires publishing, such as `implement-spec` opening a required draft PR.

## Integration

**Called when:** Committing code, asking about branching, preparing a pull request, checking workflow rules.

**Pairs with:**
- `code-review` skill — for review before merging
- `refactoring-workflow` skill — always operates on a feature branch
- `tdd-workflow` / `with-tests-workflow` — commit after tests pass
- Matt Pocock's `implement-spec` — its integration branch and conditional PR requirements take precedence over the ordinary feature-branch and no-push defaults
- Matt Pocock's `implement` — commit locally before its `code-review` call so the review can see the implementation
