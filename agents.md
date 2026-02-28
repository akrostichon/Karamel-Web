# Custom Agents for Karamel-Web

This document provides a registry of all available custom agents for GitHub Copilot in the Karamel-Web repository. These agents provide specialized expertise for specific development tasks.

## Code Review Agent

**File**: [`.github/agents/code-review.agent.md`](.github/agents/code-review.agent.md)

**Purpose**: Comprehensively reviews all branch changes against a base branch with analysis of code quality, security, testing, performance, and architecture.

**Key Features**:
- Automatic parent branch detection using commit distance algorithm
- Safe handling of uncommitted changes (stash and restore)
- Prioritized findings (CRITICAL → IMPORTANT → SUGGESTION)
- Project-specific checks (security, testing, architecture)
- Actionable feedback with code examples
- Integration with code review standards

**When to use**:
- Before submitting pull requests for merge to main/develop
- To validate code quality and security compliance
- To ensure test coverage requirements are met
- To check architectural patterns and consistency

**How to invoke**:
- Open the repository in VS Code
- Switch to your feature branch
- Use the Code Review Agent from the Copilot custom agents list
- Agent will auto-detect your base branch and analyze all changes

**Expected output**:
- Structured review report with categorized findings
- File-by-file analysis with specific line references
- Suggestions for fixes with code examples
- Options to save report, fix issues, or view findings

**Prerequisites**:
- Must be on a feature branch (not main/develop/master)
- Git repository with clean history (commits and uncommitted changes supported)

## code-review

**File**: [`.github/skills/code-review/SKILL.md`](.github/skills/code-review/SKILL.md)

**Purpose**: Provides the review methodology used by the Code Review Agent: priorities, quality standards, security checklist, testing standards, comment format, and the full review checklist.

**Activated by**: `code-review.agent.md` (loaded as part of Step 5 — Execute Code Review).

---

# Workflow Skills

Skills are pluggable implementation strategies loaded by prompts on demand. They live in `.github/skills/<skill-name>/SKILL.md` and are activated automatically based on the user's request or `--workflow` parameter.

See `.github/instructions/agent-skills.instructions.md` for authoring guidelines.

## tdd-workflow

**File**: [`.github/skills/tdd-workflow/SKILL.md`](.github/skills/tdd-workflow/SKILL.md)

**Purpose**: Implements plan steps using Red → Green → Refactor TDD cycle.

**Activated by**: `implement-plan` prompt (default workflow), or when user requests TDD or test-first development.

**How it works**:
1. Writes a lightweight Test Intent substep into the plan markdown
2. Follows Red → Green → Refactor for each behavior

## with-tests-workflow

**File**: [`.github/skills/with-tests-workflow/SKILL.md`](.github/skills/with-tests-workflow/SKILL.md)

**Purpose**: Implements plan steps by writing production code first, then adding tests to validate behavior.

**Activated by**: `implement-plan` (default workflow), or `implement-plan --workflow=with-tests`.

**How it works**:
1. Writes a lightweight Test Intent substep into the plan markdown
2. Implements the production code
3. Writes tests after implementation
4. Runs the relevant test suite

## refactoring-workflow

**File**: [`.github/skills/refactoring-workflow/SKILL.md`](.github/skills/refactoring-workflow/SKILL.md)

**Purpose**: Implements refactoring plan steps, relying on the existing test suite as the safety net.

**Activated by**: `refactoring-plan` prompt.

**How it works**:
1. Implements each refactoring step
2. Applies Karamel-Web-specific refactoring rules (test migration when splitting files, combining blocked steps)
3. Validates using existing tests — no new tests required unless behavior is added

## brainstorming

**File**: [`.github/skills/brainstorming/SKILL.md`](.github/skills/brainstorming/SKILL.md)

**Purpose**: Explores user intent, requirements and design before any creative work (features, components, functionality, or behavior changes).

**Activated by**: Before starting feature development, component creation, or behavior modifications.

**How it works**:
1. Explores and clarifies user intent and requirements
2. Validates assumptions and edge cases
3. Designs solution before implementation begins
4. Documents decisions and alternatives

## systematic-debugging

**File**: [`.github/skills/systematic-debugging/SKILL.md`](.github/skills/systematic-debugging/SKILL.md)

**Purpose**: Systematically identifies root causes of bugs, test failures, and unexpected behavior before attempting fixes.

**Activated by**: When encountering any technical issue (test failures, bugs, unexpected behavior, build failures).

**How it works**:
1. Investigates root cause first (never proposes fixes without root cause analysis)
2. Documents symptoms and their causes
3. Verifies the fix actually resolves the root cause
4. Validates there are no side effects

---

## Adding New Agents

To add a new custom agent to the repository:

1. Create a new `.agent.md` file in `.github/agents/` directory
2. Follow guidelines in [`.github/instructions/agents.instructions.md`](.github/instructions/agents.instructions.md)
3. Include proper YAML frontmatter with description, model, tools, and target
4. Document the agent's purpose, capabilities, and usage in this file
5. Add entry to the registry below with consistent formatting

## Agent Registry Template

```markdown
### [Agent Name]

**File**: `[path to agent file]`

**Purpose**: [Single-sentence description of what the agent does]

**Key Features**:
- [Feature 1]
- [Feature 2]
- [Feature 3]

**When to use**:
- [Scenario 1]
- [Scenario 2]

**How to invoke**:
[Instructions for using the agent]

**Expected output**:
[Description of typical agent output/recommendations]

**Prerequisites**:
- [Requirement 1]
- [Requirement 2]
```
