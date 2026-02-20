---
agent: agent
description: Implement a refactoring plan from a provided markdown file
argument-hint: "Provide a markdown file with a detailed refactoring plan, and optional step number"
---

You are implementing refactoring steps from the markdown file provided in context. Follow these instructions precisely.

> **Branch and commit rules** are enforced globally by `.github/instructions/git-workflow.instructions.md` — do not re-state them here.

## Rules

1. **Clarification First**
   - If requirements are ambiguous, ask for clarification before implementing
   - If multiple valid approaches exist, present options and wait for user choice
   - Never assume requirements

## Orchestration Steps

Execute in order:

1. **Validate Target** — Read the markdown file, confirm the target step(s) exist, identify what must be refactored.

2. **Invoke Refactoring Skill** — Follow `.github/skills/refactoring-workflow/SKILL.md` for implementation and validation strategy.

3. **Update Documentation** — Mark completed step(s) in the markdown.

4. **Commit** — Use a descriptive, user-facing commit message. Do not push unless explicitly requested.

5. **Report Results**
   - Files changed (with paths)
   - Commands run for validation
   - Any remaining TODOs or risks

## Acceptance Criteria

- [ ] All existing tests still pass after each refactoring step
- [ ] Target step(s) marked completed in the markdown
- [ ] Local commit uses a descriptive, user-facing message

## Examples

- `/refactoring-plan plan-sessionServiceRefactor.md` — implement all steps
- `/refactoring-plan plan-sessionServiceRefactor.md step 3` — implement only step 3
