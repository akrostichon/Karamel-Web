---
agent: agent
description: Implement the refactoring plan from a provided markdown
argument-hint: "Provide a markdown file with a detailed refactoring plan"
---

You are implementing plan provided within the markdown file from context. Follow these instructions precisely:

## CRITICAL RULES

1. **Branch Management**
   - Check current git branch first
   - If on `main`, create feature branch: `feature/user-facing-name-of-implementation-goal`
   - If on another branch, stay on that branch

2. **IMPLEMENTATION**
   - Implement the steps in the plan.
   - Follow existing project patterns and architecture while implementing
   - If you encounter any blockers or uncertainties during implementation, report them immediately for clarification.
   - If you are removing a file and splitting it into multiple files, migrate the tests as well, but check first that this is not poart of a later step in the plan.
   - if you are blocked and implementing a later step in the plan together with the step you are currently implementing would unblock you, implement both steps together, but notify the user about it and explain the reason.
 
3. **Commit Policy**
   - Use user-facing commit messages (e.g., "Add Azure deployment pipeline", not "Implement Phase 3")
   - Commit locally by default — only push if explicitly requested
   - Never commit `plan*.md` files (including `plan-*.md`); they are git-ignored planning notes

## IMPLEMENTATION STEPS

Execute these steps in order for the target markdown file:

1. **Validate Target**
   - Read the markdown file and confirm the target exists
   - Identify all steps within it

2. **IMPLEMENTATION**
   - Implement the steps in the plan.

3. **Validate Locally**
   - Perform checks required by the plan to validate your changes (e.g., run tests, run build)

4. **Update Documentation**
   - Mark Step(s) as completed in the target markdown
   - Report brief summary to user

5. **Commit Implementation**
   - Use descriptive, user-facing commit message
   - Do NOT push unless explicitly requested

6. **Report Results**
   - List files changed (with paths)
   - Show commands run for validation
   - Note any remaining TODOs or risks

## EXAMPLES

- `/refactoring-plan Future_requirements.md` — Implement every step within the future_requirements.md
- `/refactoring-plan Future_requirements.md step 1` — Implement only step 1 in future_requirements.md (still plan and implement tests for it)
