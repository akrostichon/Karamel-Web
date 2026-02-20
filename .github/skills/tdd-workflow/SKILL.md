---
name: tdd-workflow
description: Test-Driven Development workflow using Red-Green-Refactor for implementing plan steps. Use when asked to implement code with TDD, write tests first, follow Red-Green-Refactor, or when the implement-plan prompt is run with --workflow=tdd. Covers C# (xUnit, bUnit) and JavaScript (Vitest) test creation for Karamel-Web.
---

# TDD Workflow

Implements plan steps using **Test-Driven Development**: tests are written before production code, following the Red → Green → Refactor cycle.

## Design First — Goal Image

Before writing the first test, briefly establish a **Goal Image** for the design:

- Sketch a simple target structure: main responsibilities, key boundaries (UI vs. application vs. infrastructure), key data flows
- Keep it lightweight — 1–5 bullet points is enough
- Use it as a **compass, not a contract**: update it if TDD reveals a better design
- Do **not** over-design upfront (no exhaustive diagrams or speculative abstractions)

## Step 1 — Write a Lightweight Test Intent into the Markdown

Before writing any code, add a **"Tests" substep** to the target plan step in the markdown.

Include:
- Key **behaviors** that must be covered (what to verify, not how)
- The **test level**: C# unit (xUnit), Blazor component (bUnit), JavaScript unit (Vitest), or integration

Note explicitly when:
- No testable behavior exists: `"No testable behavior identified — tests ruled out"`
- Manual verification is the right approach: `"Manual testing required: [brief description]"`

## Step 2 — Red → Green → Refactor

For each behavior in the test intent:

1. **Red** — Write one failing test. Search existing test files first; modify a relevant test if found rather than duplicating. If existing code lacks tests, add **characterization tests** before changing behavior.
2. **Green** — Write the minimal production code to make it pass.
3. **Refactor** — Clean up before moving on. Never skip refactoring even if the code appears to work — messy green code is a design smell.
4. Repeat for the next behavior.

Keep tests focused and atomic (one behavior per test). Test **observable behavior**, not private implementation.

### Signals to Stop and Refactor

If you encounter any of these, refactor toward testability before proceeding:
- "This is hard to test"
- Excessive mocking complexity
- Many side effects in one method
- Tight coupling between components

## Step 3 — Run Tests

Run only the tests relevant to the changes made. Do not run the full suite unless necessary.

| Changed area | Command |
|---|---|
| C# frontend / Blazor | `dotnet test Karamel.Web.Tests` |
| JavaScript | `cd Karamel.Web/wwwroot && npm run test:run` (then `cd ../..`) |
| C# backend | Ask the user to run `dotnet test Karamel.Backend.Tests -v minimal` manually |

Fix all failures before proceeding. A skipped test is acceptable only when explicitly justified.

## Testing Conventions

### Test Naming

| Language | Pattern |
|---|---|
| C# (xUnit) | `MethodName_State_ExpectedResult` |
| JS (Vitest) | `it('does X when Y', () => {...})` |

### Testing by Layer

**C# — ASP.NET Core (xUnit)**
- Default to xUnit for all backend logic
- Prefer pure domain/service tests over controller tests
- Use dependency injection and interfaces for isolation
- Use `WebApplicationFactory` for HTTP endpoints only when needed

```csharp
[Fact]
public void CalculateTotal_WithTax_ReturnsCorrectAmount()
{
    var sut = new PriceService();
    var result = sut.CalculateTotal(100m, 0.19m);
    Assert.Equal(119m, result);
}
```

**Blazor — UI (bUnit)**
- Test rendered output and interactions, not internal state
- Mock services via interfaces

```csharp
[Fact]
public void ShowsErrorMessage_WhenSubmissionFails()
{
    using var ctx = new TestContext();
    var cut = ctx.RenderComponent<MyForm>();
    cut.Find("button").Click();
    cut.Markup.Contains("Error");
}
```

**JavaScript — ES Modules (Vitest)**
- Prefer small, pure functions where possible
- Mock external dependencies explicitly

```javascript
import { describe, it, expect } from 'vitest';
import { sum } from './math.js';

describe('sum', () => {
  it('adds numbers', () => {
    expect(sum(1, 2)).toBe(3);
  });
});
```

### Code Standards for Testability

Make code easy to test:
- Small methods with clear inputs/outputs
- Dependency injection (ASP.NET Core built-in DI)
- Interfaces for external dependencies
- Pure functions where practical

Avoid: hidden global state, hard-coded dependencies, static singletons.

When refactoring, optimize for: readability, cohesion, loose coupling.

## When You May Deviate (Use Sparingly)

Write code before tests only when:
- Prototyping a spike explicitly labeled as "exploratory (no tests)"
- The user explicitly requests "no tests"

Otherwise, default back to TDD immediately.
