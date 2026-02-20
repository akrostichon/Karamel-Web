---
name: with-tests-workflow
description: Implementation-first workflow for plan steps where code is written before tests. Use when explicitly asked to implement without TDD, skip the Red-Green-Refactor cycle, implement first and test after, or when the implement-plan prompt is run with --workflow=with-tests (the default). Tests are still written and must pass — just after the implementation rather than before.
---

# With-Tests Workflow

Implements plan steps by **writing production code first**, then adding tests to cover the key behaviors. Tests are not optional — they are written after implementation rather than before.

## Step 1 — Write a Lightweight Test Intent into the Markdown

Add a **"Tests" substep** to the target plan step in the markdown before writing any code. Keep it brief.

Include:
- Key **behaviors** that need test coverage after implementation
- The **test level**: C# unit (xUnit), Blazor component (bUnit), JavaScript unit (Vitest), or integration

## Step 2 — Implement

Write the production code for the plan step.

- Follow existing project patterns and architecture
- If you encounter blockers or ambiguities, report them immediately before continuing
- Keep the implementation aligned with the test intent you wrote in Step 1

## Step 3 — Write Tests

After the implementation is working, write tests for the behaviors listed in the test intent.

- Search existing test files first — modify a relevant test rather than creating a duplicate
- Keep tests focused: one behavior per test
- Use mocks and dependency injection for isolation

## Step 4 — Run Tests

Run only the tests relevant to the changes made. Do not run the full suite unless necessary.

| Changed area | Command |
|---|---|
| C# frontend / Blazor | `dotnet test Karamel.Web.Tests` |
| JavaScript | `cd Karamel.Web/wwwroot && npm run test:run` (then `cd ../..`) |
| C# backend | `dotnet test Karamel.Backend.Tests -v minimal` |

Fix all failures before proceeding.

## Testing Conventions

### Test Naming

| Language | Pattern |
|---|---|
| C# (xUnit) | `MethodName_State_ExpectedResult` |
| JS (Vitest) | `it('does X when Y', () => {...})` |

### Testing by Layer

**C# — ASP.NET Core backend (xUnit)** → test files in `Karamel.Backend.Tests/`
- Prefer pure domain/service tests over controller tests
- Use dependency injection and interfaces for isolation
- Use `WebApplicationFactory` for HTTP endpoints only when needed

**Blazor — UI components (bUnit)** → test files in `Karamel.Web.Tests/`
- Test rendered output and user interactions, not internal component state
- Mock services via interfaces

**JavaScript — ES Modules (Vitest)** → test files in `Karamel.Web/wwwroot/js/*.test.js`
- Prefer small, pure functions
- Mock external dependencies explicitly

### Code Standards for Testability

- Small methods with clear inputs/outputs
- Dependency injection (ASP.NET Core built-in DI)
- Interfaces for external dependencies
- Avoid: hidden global state, hard-coded dependencies, static singletons
