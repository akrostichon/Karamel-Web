---
name: refactoring-workflow
description: Refactoring workflow for restructuring existing code without adding new features. Use when asked to refactor, restructure, reorganize, or rename code, or when the refactoring-plan prompt is invoked. Validates changes using the existing test suite rather than requiring new tests. Contains Karamel-Web-specific rules for migrating tests when splitting files and combining blocked steps.
---

# Refactoring Workflow

Implements refactoring plan steps by **restructuring existing code** while keeping behavior unchanged. The existing test suite is the safety net — run it rather than writing new tests for every step.

## Step 1 — Implement the Refactoring Step

- Follow the plan strictly
- Follow existing project patterns and architecture
- If you encounter blockers or ambiguities, report them **immediately** and ask whether you may continue.

### Special Rule: Splitting Files → Migrate Tests

When a step removes a file and splits it into multiple files, **migrate the associated tests** to match the new structure. Before doing so: check whether a later step in the plan explicitly handles test migration for that file. If it does, leave the tests alone and note this in your report.

### Special Rule: Combining Blocked Steps

If implementing the current step is blocked because a later step must be done first (e.g., a dependency exists between them), you may implement both steps together. **Notify the user** about the combination and explain why the later step was pulled forward.

## Step 2 — Validate

Run the validation checks required by the plan (typically the existing test suite and the build). Only run what the plan specifies — refactoring steps should not require the full suite unless explicitly requested.

All existing tests must remain green after each refactoring step. A regression in the existing suite indicates the refactoring changed behavior — investigate before continuing.
