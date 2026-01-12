## /implement-phase — Assistant prompt for implementing a Phase/Step

Purpose
- A concise, actionable prompt for an AI assistant to implement a specific Step or an entire Phase from the project's `DEVELOPMENT_PLAN.md`.

Usage
- Run with a single positional argument identifying the target:
  - `/implement-phase 1.2` — implement Step 1.2 only.
  - `/implement-phase 1` — implement the whole Phase 1 (all steps in that phase).

High-level rules
- Do not start implementation until the plan and tests-substep are created and committed.
- Create a feature branch only when the current git branch is `main`. Do not create a branch if on another branch.
- When you create a branch, name it `feature/implement-phase-<phase>-step-<step>` (for example `feature/implement-phase-1-step-2`) unless instructed otherwise.
- Always add a tests substep under the chosen Step X in `DEVELOPMENT_PLAN.md` before writing code. The tests substep must describe which tests will be added (unit, integration, JS), target files, and the acceptance criteria.
- Avoid running long-running or background integration tests automatically. Do not run backend SignalR/WebSocket integration tests without explicit permission.
- Avoid assumptions: when requirements are ambiguous or incomplete, explicitly ask the user for clarification before implementing. If multiple valid implementations exist, present the reasonable options and request which to pursue.
- When committing, use a user-facing commit message that describes the feature or user-visible change (example: "Azure provisioning & Deployment"), not a vague technical message like "Implement Phase X".
- Create commits and push only when explicitly asked to; by default commit locally and stop.

Checklist for the assistant (step-by-step)
1. Validate input
   - Confirm the requested target is valid (exists in `DEVELOPMENT_PLAN.md`). If ambiguous, ask the user which exact Step or Phase is intended.
2. Inspect current git branch
   - If branch is `main`, create the feature branch as described in rules. If not `main`, stay on the current branch.
3. Plan tests (required)
   - Add a new substep to the target Step X in `DEVELOPMENT_PLAN.md` called "Tests".
   - For each test target include:
     - Test type: `C# unit` (xUnit/bUnit), `C# integration` (use TestServer; only run with permission), `JS unit` (Vitest).
     - File(s) to add or update (paths relative to repo).
     - Mocks or fixtures required.
     - Acceptance criteria (e.g., "All unit tests in `Karamel.Web.Tests` pass locally; JS Vitest suite passes").
   - Commit the plan update with a descriptive commit message: "Plan tests for Phase X — Step Y: <short description>".
4. Implement tests
   - Create test skeletons and helpers in appropriate test projects.
   - Keep tests focused and small. Use dependency injection and mocks where appropriate.
5. Implement feature code
   - Make minimal, root-cause changes necessary to satisfy the tests and acceptance criteria.
6. Run tests (locally)
   - Run C# tests: `dotnet test Karamel.Web.Tests` and/or `dotnet test Karamel.Backend.Tests` (do not run backend integration tests automatically).
   - Run JS tests: `cd Karamel.Web/wwwroot && npm run test:run`.
   - Fix failures until acceptance criteria are met.
7. Update `DEVELOPMENT_PLAN.md`
   - Mark the Step X as completed (check the checkbox and add a short note summarizing the tests run and results).
8. Commit changes
   - Use a human-facing commit message describing the feature from the user's perspective.
   - Do not push unless explicitly requested.
9. Report
   - Produce a short report listing:
     - Files changed (with paths)
     - Tests added/updated
     - Commands run to validate
     - Any remaining TODOs or risks

Acceptance criteria
- The tests described in the plan exist and run locally, with passing results (tests previously skipped may remain skipped).
- The step in `DEVELOPMENT_PLAN.md` is marked completed.
- The local git history contains descriptive commits covering planning, tests, and implementation.

Examples
- Minimal: `/implement-phase 2.10` — create tests substep, add tests, implement PlayerView fixes, run `Karamel.Web.Tests`, update plan, commit locally.
- Whole phase: `/implement-phase 1` — iterate through all steps in Phase 1, adding tests-substeps for each step and implementing them one-by-one.

Notes & constraints
- Always ask if a requested step requires external resources you cannot access (CI, production database, Azure). Obtain explicit permission before running remote or long-running integration tasks.
- If any action would modify production resources or sensitive configuration, stop and ask for credentials/confirmation.
- For C# backend integration tests involving SignalR or WebApplicationFactory, ask the user before running; do not run them by default.
