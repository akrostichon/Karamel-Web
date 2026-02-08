---
agent: agent
description: Implement a specific Phase or Step...
argument-hint: "Phase or Step identifier (e.g., '1' or '1.2')"
---

You are implementing the specified Phase or Step from `DEVELOPMENT_PLAN.md`. Follow these instructions precisely:

## CRITICAL RULES

1. **Branch Management**
   - Check current git branch first
   - If on `main`, create feature branch: `feature/implement-phase-<phase>-step-<step>`
   - If on another branch, stay on that branch

2. **Clarification First**
   - If requirements are ambiguous, ask for clarification before implementing
   - If multiple valid approaches exist, present options and wait for user choice
   - Never assume requirements

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

## IMPLEMENTATION STEPS

Execute these steps in order for the target Phase/Step:

1. **Validate Target**
   - Read `DEVELOPMENT_PLAN.md` and confirm the target exists
   - If implementing a full Phase, identify all steps within it

2. **Create Lightweight Test Intent**
   - Add a brief "Tests" substep to the markdown that captures:
     - Key behaviors that need test coverage
     - Intended test level (unit / bUnit / JS / integration)
   - Keep this concise — detailed test design will emerge through TDD.

3. **Test Driven Development**
   - Follow `.github/instructions/tdd.instructions.md` for how to apply TDD.
   - Create test files in appropriate projects (`Karamel.Web.Tests`, `Karamel.Backend.Tests`, or `wwwroot/js/*.test.js`)
   - Use mocks and dependency injection appropriately
   - Keep tests focused and atomic

4. **Implement Feature**
   - Write minimal code to satisfy tests and acceptance criteria
   - Follow existing project patterns and architecture

5. **Validate Locally**
   - Run C# frontend tests: `dotnet test Karamel.Web.Tests`
   - Run JS tests: `cd Karamel.Web/wwwroot && npm run test:run`
   - For backend changes: Ask user to manually run `dotnet test Karamel.Backend.Tests`
   - Fix failures until acceptance criteria met

6. **Update Documentation**
   - Mark Step(s) as completed in `DEVELOPMENT_PLAN.md`
   - Add brief summary of tests run and results

7. **Commit Implementation**
   - Use descriptive, user-facing commit message
   - Do NOT push unless explicitly requested

8. **Report Results**
   - List files changed (with paths)
   - List tests added/updated
   - Show commands run for validation
   - Note any remaining TODOs or risks

## ACCEPTANCE CRITERIA

Before completing, verify:
- [ ] All planned tests exist and pass (or are intentionally skipped)
- [ ] Target Step(s) marked completed in `DEVELOPMENT_PLAN.md`
- [ ] Local commits use descriptive, user-facing messages
- [ ] No code pushed unless explicitly requested

## EXAMPLES

- `/implement-phase 1.2` — Implement only Step 1.2
- `/implement-phase 1` — Implement all steps in Phase 1 sequentially
