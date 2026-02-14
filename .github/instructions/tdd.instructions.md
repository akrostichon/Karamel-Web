---
applyTo: \*\*/\*.cs, \*\*/\*.razor, \*\*/\*.js, \*\*/\*.css
description: "Require and guide Test-Driven Development (TDD) for C#
  (ASP.NET Core + Blazor), JavaScript (ES modules), and CSS using xUnit,
  bUnit, and Vitest, while planning with a flexible \"Goal Image\"."
---

# Test-Driven Development (TDD) --- C# / Blazor / JavaScript

These instructions require GitHub Copilot to develop using **Test-Driven
Development (TDD)** by default for all production code changes in: -
**C#**: ASP.NET Core backend + Blazor frontend - **JavaScript**: ES
modules - **CSS** - **Testing**: xUnit, bUnit (Blazor), Vitest
(JavaScript)

## General Instructions

-   **Always use TDD unless explicitly told otherwise.**
-   Follow the cycle: **Red → Green → Refactor** for every change.
-   Prefer **many small steps** over few large changes.
-   Treat tests as a primary design tool, not an afterthought.
-   If a request is unclear, make a reasonable TDD-based assumption and
    proceed (do not skip tests).

## Design First --- but Stay Flexible (Goal Image)

Before writing the first test, briefly establish a **Goal Image** for
the design:

-   Sketch (mentally or in comments) a **simple target structure**:
    -   Main responsibilities of components/services
    -   Likely boundaries (UI vs. application vs. infrastructure)
    -   Key data flows
-   Keep this lightweight (1--5 bullet points is enough).
-   Use the Goal Image as a **compass, not a contract**:
    -   The design may and should evolve as tests drive discoveries.
    -   If TDD reveals a better design, **update the Goal Image and
        follow the better path.**
-   Do **not** over-design up front (no exhaustive diagrams or
    speculative abstractions).

## Workflow (Mandatory)

For any new behavior, bug fix, or refactor:

1.  **Clarify the Goal Image (very briefly).**
2.  **Write a failing test first (Red). Search for existing tests that might cover the new behavior and modify them if necessary.**
3.  **Make the test pass (Green)** with the simplest code that works.
4.  **Refactor safely (Refactor)** and adjust the Goal Image if needed. Do NOT skip refactoring. Even if the code works, it may be a sign of design issues that need addressing.
5.  Repeat.

> If existing code lacks tests: add **characterization tests** before
> changing behavior.

## Testing Strategy by Layer

### C# --- ASP.NET Core (xUnit)

-   Default to **xUnit** for all backend logic.
-   Prefer pure domain/services tests over controller tests when
    possible.
-   Use dependency injection and interfaces to enable isolation.
-   For HTTP endpoints, use **WebApplicationFactory** only when needed.

**Good**

``` csharp
public class PriceServiceTests
{
    [Fact]
    public void CalculatesTotalWithTax()
    {
        var sut = new PriceService();
        var result = sut.CalculateTotal(100m, 0.19m);
        Assert.Equal(119m, result);
    }
}
```

### Blazor --- UI (bUnit)

-   Use **bUnit** for component behavior.
-   Test **rendered output and interactions**, not internal state.
-   Mock services via interfaces.

**Good**

``` csharp
[Fact]
public void ShowsErrorMessage_WhenSubmissionFails()
{
    using var ctx = new TestContext();
    var cut = ctx.RenderComponent<MyForm>();
    cut.Find("button").Click();
    cut.Markup.Contains("Error");
}
```

### JavaScript --- ES Modules (Vitest)

-   Use **Vitest** for all JS tests.
-   Prefer small, pure functions where possible.
-   Mock external dependencies explicitly.

**Good**

``` javascript
import { describe, it, expect } from 'vitest';
import { sum } from './math.js';

describe('sum', () => {
  it('adds numbers', () => {
    expect(sum(1, 2)).toBe(3);
  });
});
```

### CSS

-   Prefer behavior-driven tests via components (bUnit) or visual
    snapshots if the project uses them.
-   Avoid testing implementation details of styles directly.

## Test Design Rules

-   One test = one behavior.
-   Tests must be fast, deterministic, and isolated.
-   Avoid shared mutable state between tests.
-   Test **observable behavior**, not private implementation.

### Naming Conventions (Tests)

  Language      Pattern
  ------------- ------------------------------------
  C# (xUnit)    `MethodName_State_ExpectedResult`
  JS (Vitest)   `it('does X when Y', () => {...})`

**Good**

``` csharp
[Fact]
public void CalculateTotal_WithTax_ReturnsCorrectAmount() { }
```

## Code Standards Related to TDD & Design

-   Make code **easy to test**: small methods, clear inputs/outputs.
-   Avoid:
    -   Hidden global state
    -   Hard-coded dependencies
    -   Static singletons that block testing
-   Prefer:
    -   Dependency injection (ASP.NET Core built-in DI)
    -   Interfaces for external dependencies
    -   Pure functions where practical
-   When refactoring, optimize for:
    -   Readability
    -   Cohesion (related behavior together)
    -   Loose coupling between modules

## Common Patterns

### Characterization Test (before refactor)

``` csharp
[Fact]
public void ExistingBehavior_DoesNotChange()
{
    Assert.Equal(42, LegacyService.Compute(5));
}
```

### Incremental Growth

1.  Add a minimal failing test or check for existing test that will fail and modify it.
2.  Make it pass.
3.  Add the next test for the next behavior.

## When You May Deviate (Use Sparingly)

You may write code before tests only when: - Prototyping a spike
explicitly labeled as **"exploratory (no tests)"** - Working in a
REPL/notebook for discovery - The user explicitly requests "no tests"

Otherwise, default back to TDD immediately.

## Review & Verification (for PR-style outputs)

Include: - ✅ Tests added/updated (xUnit / bUnit / Vitest as
appropriate) - ✅ All tests passing - ✅ Brief note on the Goal Image
and any changes to it - ✅ Brief note on what was refactored (if
anything)


## Signals to Improve Design

If you encounter any of these, refactor toward testability before
proceeding: - "This is hard to test" - Excessive mocking complexity -
Many side effects in one method - Tight coupling between components


