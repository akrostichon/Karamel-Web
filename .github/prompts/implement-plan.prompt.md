---
agent: agent
description: Implement the plan from a provided markdown
argument-hint: "Provide a markdown file with a detailed implementation plan"
---

You are implementing plan provided within the markdown file from context. Follow these instructions precisely:

## CRITICAL RULES

1. **Branch Management**
   - Check current git branch first
   - If on `main`, create feature branch: `feature/user-facing-name-of-implementation-goal`
   - If on another branch, stay on that branch

2. **Clarification First**
   - If requirements are ambiguous, ask for clarification before implementing
   - If multiple valid approaches exist, present options and wait for user choice
   - Never assume requirements
   - feed clarifications back into the markdown file

3. **Test-Driven Development (MANDATORY)**
   - Follow `.github/instructions/tdd.instructions.md` as the primary source of truth for testing and TDD workflow.
   - Before implementation, add a **lightweight "Tests" substep** to the target markdown that states:
     - The **main behaviors that must be covered by tests** (what, not how)
     - The **test category** (C# unit, bUnit, JS unit, or integration) — high level only
   - Do **NOT** require:
     - Exhaustive test lists upfront  
     - Exact test file paths before code exists  
     - Detailed mocks/fixtures before seeing the design  
   - Then implement using **Red → Green → Refactor**, adding tests incrementally rather than as one big upfront plan.

4. **Commit Policy**
   - Use user-facing commit messages (e.g., "Add Azure deployment pipeline", not "Implement Phase 3")
   - Commit locally by default — only push if explicitly requested
   - Never commit `plan*.md` files (including `plan-*.md`); they are git-ignored planning notes

## IMPLEMENTATION STEPS

Execute these steps in order for the target markdown file:

1. **Validate Target**
   - Read the markdown file and confirm the target exists
   - Identify all steps within it

2. **Create Lightweight Test Intent**
   - Add a brief "Tests" substep to the markdown that captures:
     - Key behaviors that need test coverage
     - Intended test level (unit / bUnit / JS / integration)
     - in case no testable behavior is identified, explicitly state "No testable behavior identified for this step" to confirm that tests were considered and ruled out.
     - in case of required manual testing, explicitly state "Manual testing required for this step: [brief description of what to verify manually]" to confirm that manual testing was considered and deemed necessary.
   - Keep this concise — detailed test design will emerge through TDD.

3. **Test Driven Development**
   - Follow `.github/instructions/tdd.instructions.md` for how to apply TDD.
   - Create test file in appropriate projects (`Karamel.Web.Tests`, `Karamel.Backend.Tests`, or `wwwroot/js/*.test.js`)
   - Use mocks and dependency injection appropriately
   - Keep tests focused and atomic
   - Write single test and implement minimal code to pass it before moving to the next
   - Follow existing project patterns and architecture while implementing

4. **Validate Locally**
   - Only run tests relevant to the changes made (do not run the entire suite unless necessary)
   - Run C# frontend tests: `dotnet test Karamel.Web.Tests`
   - Run JS tests: `cd Karamel.Web/wwwroot && npm run test:run`
   - For backend changes: Ask user to manually run `dotnet test Karamel.Backend.Tests`
   - Fix failures until acceptance criteria met

5. **Update Documentation**
   - Mark Step(s) as completed in the target markdown
   - Report brief summary to user

6. **Commit Implementation**
   - Use descriptive, user-facing commit message
   - Do NOT push unless explicitly requested

7. **Report Results**
   - List files changed (with paths)
   - List tests added/updated
   - Show commands run for validation
   - Note any remaining TODOs or risks

## ACCEPTANCE CRITERIA

Before completing, verify:
- [ ] All planned tests exist and pass (or are intentionally skipped)
- [ ] Target Step(s) marked completed in target markdown
- [ ] Local commits use descriptive, user-facing messages

## EXAMPLES

- `/implement-plan Future_requirements.md` — Implement every step within the future_requirements.md
- `/implement-plan Future_requirements.md step 1` — Implement only step 1 in future_requirements.md (still plan and implement tests for it)
