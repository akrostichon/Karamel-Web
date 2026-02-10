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
