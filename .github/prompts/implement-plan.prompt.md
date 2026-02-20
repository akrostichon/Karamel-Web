---
agent: agent
description: Implement plan steps from a provided markdown file, with a choice of implementation-first (with-tests) or TDD workflow
argument-hint: "Provide a markdown file, optional step number, and optional --workflow=with-tests|tdd"
---

You are implementing steps from the markdown file provided in context. Follow these instructions precisely.

> **Branch and commit rules** are enforced globally by `.github/instructions/git-workflow.instructions.md` — do not re-state them here.

## Parameters

| Parameter | Values | Default | Description |
|---|---|---|---|
| `file` | any `.md` path | *(required)* | The plan markdown to read |
| `step` | number or range | all steps | Limit implementation to a specific step |
| `--workflow` | `with-tests`, `tdd` | `with-tests` | Implementation strategy skill to invoke |

## Rules

1. **Clarification First**
   - If requirements are ambiguous, ask for clarification before implementing
   - If multiple valid approaches exist, present options and wait for user choice
   - Never assume requirements
   - Feed clarifications back into the markdown file

## Orchestration Steps

Execute in order:

1. **Validate Target** — Read the markdown file, confirm the target step(s) exist, identify what must be implemented.

2. **Invoke Workflow Skill** — Apply the selected `--workflow` skill:
   - `with-tests` → follow `.github/skills/with-tests-workflow/SKILL.md`
   - `tdd` → follow `.github/skills/tdd-workflow/SKILL.md`

3. **Update Documentation** — Mark completed step(s) in the markdown.

4. **Commit** — Use a descriptive, user-facing commit message. Do not push unless explicitly requested.

5. **Report Results**
   - Files changed (with paths)
   - Tests added or updated
   - Commands run for validation
   - Any remaining TODOs or risks

## Acceptance Criteria

- [ ] All planned tests exist and pass (or are explicitly justified as skipped)
- [ ] Target step(s) marked completed in the markdown
- [ ] Local commit uses a descriptive, user-facing message

## Examples

- `/implement-plan plan-fixSessionDataRetention.md` — implement all steps, code first then tests (default)
- `/implement-plan FUTURE_REQUIREMENTS.md step 2` — implement only step 2 with the default workflow
- `/implement-plan FUTURE_REQUIREMENTS.md --workflow=tdd` — implement all steps with TDD (Red-Green-Refactor)
