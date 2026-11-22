# Custom Agents for Karamel-Web

This document provides a registry of all available custom agents for GitHub Copilot in the Karamel-Web repository. These agents provide specialized expertise for specific development tasks.

## Code Review Agent

**File**: [`.github/agents/code-review.agent.md`](.github/agents/code-review.agent.md)

**Purpose**: Comprehensively reviews all branch changes against a base branch with analysis of code quality, security, testing, performance, and architecture.

**Key Features**:
- Automatic parent branch detection using commit distance algorithm
- Prioritized findings (CRITICAL → IMPORTANT → SUGGESTION)
- Karamel-Web-specific checks through the project review supplement
- Actionable feedback with code examples

**When to use**:
- Before submitting pull requests for merge to main/develop
- To review committed branch changes against the detected base branch

**How to invoke**:
- Open the repository in VS Code
- Use the Code Review Agent from the Copilot custom agents list
- The agent detects the base branch and reviews the branch diff

**Expected output**:
- Structured review report with categorized findings
- File-by-file analysis with specific line references
- Suggestions for fixes with code examples
- Option to save the report

**Prerequisites**:
- Must be on a feature branch (not main/develop/master)
- All changes must be committed and pushed to the branch's upstream

## Architecture Review for Plan

**File**: [`.github/agents/architecture-review-for-plan.agent.md`](.github/agents/architecture-review-for-plan.agent.md)

**Purpose**: Reviews an implementation plan's architecture, risks, and alternatives.

**When to use**: Before implementing a plan that makes significant architectural decisions.

**How to invoke**: Select Architecture Review for Plan from the Copilot custom agents list.

## Karamel code-review supplement

**File**: [`.github/skills/karamel-code-review/SKILL.md`](.github/skills/karamel-code-review/SKILL.md)

**Purpose**: Supplies Karamel-Web-specific review invariants for session isolation, file-path privacy, serialization, playlist state, and hosting.

**Used by**: The repository Code Review Agent, which references this supplement in Step 5.

---

# Skills

Project skills live in `.github/skills/`. Their descriptions and the plan prompts define when each applies; implementation strategies can also be selected with `implement-plan --workflow=...`.

## implement-plan

**File**: [`.github/skills/implement-plan/SKILL.md`](.github/skills/implement-plan/SKILL.md)

**Purpose**: Implements a plan using the default with-tests workflow or `--workflow=tdd`.

## refactoring-plan

**File**: [`.github/skills/refactoring-plan/SKILL.md`](.github/skills/refactoring-plan/SKILL.md)

**Purpose**: Executes a refactoring plan using the refactoring workflow.

## tdd-workflow

**File**: [`.github/skills/tdd-workflow/SKILL.md`](.github/skills/tdd-workflow/SKILL.md)

**Purpose**: Implements plan steps using Red → Green → Refactor TDD cycle.

**Activated by**: `implement-plan --workflow=tdd`, or when the user requests TDD or test-first development.

**How it works**:
1. Writes a lightweight Test Intent substep into the plan markdown
2. Follows Red → Green → Refactor for each behavior

## with-tests-workflow

**File**: [`.github/skills/with-tests-workflow/SKILL.md`](.github/skills/with-tests-workflow/SKILL.md)

**Purpose**: Implements plan steps by writing production code first, then adding tests to validate behavior.

**Activated by**: `implement-plan` (default workflow), or `implement-plan --workflow=with-tests`.

**How it works**:
1. Writes a lightweight Test Intent substep into the plan markdown
2. Implements the production code
3. Writes tests after implementation
4. Runs the relevant test suite

## refactoring-workflow

**File**: [`.github/skills/refactoring-workflow/SKILL.md`](.github/skills/refactoring-workflow/SKILL.md)

**Purpose**: Implements refactoring plan steps, relying on the existing test suite as the safety net.

**Activated by**: `refactoring-plan` prompt.

**How it works**:
1. Implements each refactoring step
2. Applies Karamel-Web-specific refactoring rules (test migration when splitting files, combining blocked steps)
3. Validates using existing tests — no new tests required unless behavior is added

## git-workflow

**File**: [`.github/skills/git-workflow/SKILL.md`](.github/skills/git-workflow/SKILL.md)

**Purpose**: Enforces Karamel-Web git conventions: branch protection, feature branch creation, user-facing commit messages, plan file exclusion, and local-first commits.

**Activated by**: When committing code, creating branches, preparing commit messages, or asking about git workflow conventions.

**How it works**:
1. Verifies the current branch is not `main`/`develop`/`master`
2. Creates a `feature/…` branch if needed
3. Removes plan files from staging if accidentally added
4. Commits with a user-facing outcome-oriented message
5. Holds push until explicitly requested by the user

## logging-observability

**File**: [`.github/skills/logging-observability/SKILL.md`](.github/skills/logging-observability/SKILL.md)

**Purpose**: Applies backend logging conventions and guidance for inspecting Render logs.

---

## Agent reference docs

### Issue tracker

Issues are tracked in this repo's GitHub Issues; use the `gh` CLI. See [issue-tracker.md](docs/agents/issue-tracker.md).

### Triage labels

Use the canonical labels `needs-triage`, `needs-info`, `ready-for-agent`, `ready-for-human`, and `wontfix`. See [triage-labels.md](docs/agents/triage-labels.md).

### Domain docs

Use the single-context domain docs layout. See [domain.md](docs/agents/domain.md).
