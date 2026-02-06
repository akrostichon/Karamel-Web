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

2. **Test-First Approach (MANDATORY)**
   - Do NOT write implementation code until tests are planned and committed
   - Add a "Tests" substep to the target Step in `DEVELOPMENT_PLAN.md`
   - The substep MUST include:
     - Test types: C# unit (xUnit/bUnit), C# integration (TestServer), or JS unit (Vitest)
     - Target test file paths
     - Required mocks/fixtures
     - Acceptance criteria
   - Commit the test plan: "Plan tests for Phase X — Step Y: <description>"

3. **Clarification First**
   - If requirements are ambiguous, ask for clarification before implementing
   - If multiple valid approaches exist, present options and wait for user choice
   - Never assume requirements

4. **Integration Test Restrictions**
   - NEVER run backend SignalR/WebSocket tests automatically
   - Always ask permission before running C# integration tests
   - Default to running only unit tests

5. **Commit Policy**
   - Use user-facing commit messages (e.g., "Add Azure deployment pipeline", not "Implement Phase 3")
   - Commit locally by default — only push if explicitly requested

## IMPLEMENTATION STEPS

Execute these steps in order for the target Phase/Step:

1. **Validate Target**
   - Read `DEVELOPMENT_PLAN.md` and confirm the target exists
   - If implementing a full Phase, identify all steps within it

2. **Create Test Plan**
   - Add "Tests" substep to `DEVELOPMENT_PLAN.md` under target Step(s)
   - Specify all tests with file paths, types, and acceptance criteria
   - Commit: "Plan tests for Phase X — Step Y: <description>"

3. **Implement Tests**
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
