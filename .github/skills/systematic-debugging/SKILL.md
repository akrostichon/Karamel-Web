---
name: systematic-debugging
description: Use when encountering any bug, test failure, or unexpected behavior, before proposing fixes
---

# Systematic Debugging

## Overview

Random fixes waste time and create new bugs. Quick patches mask underlying issues.

**Core principles:**
1. ALWAYS find root cause before attempting fixes. Symptom fixes are failure.
2. ALWAYS ask the user when in doubt. Assumptions are silent bugs.

**Violating the letter of this process is violating the spirit of debugging.**

## The Iron Laws

```
LAW 1 — NO FIXES WITHOUT ROOT CAUSE INVESTIGATION FIRST

LAW 2 — ASK DON'T ASSUME
         Every assumption you form must be written down and confirmed
         with the user before acting on it.
         "It is better to ask one time too many than to not ask a required question."
```

If you haven't completed Phase 0, you cannot start Phase 1.
If you haven't completed Phase 1, you cannot propose fixes.

## When to Use

Use for ANY technical issue:
- Test failures
- Bugs in production
- Unexpected behavior
- Performance problems
- Build failures
- Integration issues

**Use this ESPECIALLY when:**
- Under time pressure (emergencies make guessing tempting)
- "Just one quick fix" seems obvious
- You've already tried multiple fixes
- Previous fix didn't work
- You don't fully understand the issue

**Don't skip when:**
- Issue seems simple (simple bugs have root causes too)
- You're in a hurry (rushing guarantees rework)
- Manager wants it fixed NOW (systematic is faster than thrashing)

## Default Assumption: Blame the Branch

**Unless the user explicitly states otherwise, assume the current branch's changes caused the error.**

Before anything else:
1. Run `git diff main` (or the base branch) to see ALL committed and uncommitted changes on the current branch
2. Note every modified file — these are your primary suspects
3. Look for new code paths that could match the symptom before searching elsewhere

This prevents wasting time investigating stable code when a recent change is the culprit.

## The Five Phases

You MUST complete each phase before proceeding to the next.

---

### Phase 0: Requirements Clarification

**BEFORE any investigation, validate that the bug description is complete enough to work from.**

A vague bug description leads to a wrong root cause. Don't start on unclear requirements.

**Checklist — ask the user if ANY of these are missing or ambiguous:**

| Item | What to ask if missing |
|------|------------------------|
| Reproduction steps | "What exact steps reproduce the issue?" |
| Expected behavior | "What should happen?" |
| Actual behavior | "What actually happens instead?" |
| Environment | "Which browser / OS / configuration?" |
| Branch / version | "Which branch or release is affected?" |
| Frequency | "Does it happen every time, or intermittently?" |
| Recent changes | "Did anything change before this started?" |

**Rules:**
- If the description is incomplete → stop and ask before proceeding to Phase 1
- Treat the bug description as requirements: incomplete requirements = invalid investigation
- A precise problem statement is worth more than an hour of code reading

---

### Phase 1: Root Cause Investigation

**BEFORE attempting ANY fix:**

> **Assumption Tracking Rule**: Every time you form an assumption during investigation,
> write it down explicitly ("I'm assuming X because Y") and ask the user to confirm it
> rather than treating it as fact. Do not proceed past an unverified assumption.
>
> Example: "I'm assuming this only happens when the user is not logged in. Is that correct?"

> **User as Debugger Rule**: If running the application or attaching a debugger would
> reveal the root cause faster than reading code, ask the user to do it.
> Example: "Could you open the browser console and reproduce the error? I need to see
> the full stack trace."

1. **Check Branch Changes First**
   - Run `git diff main` on the current branch (all committed + uncommitted changes)
   - Identify modified files — investigate these before other code
   - If no branch changes match the symptom, note that explicitly and ask the user whether the bug existed before this branch

2. **Read Error Messages Carefully**
   - Don't skip past errors or warnings
   - They often contain the exact solution
   - Read stack traces completely
   - Note line numbers, file paths, error codes
   - **If the stack trace is incomplete**: ask the user to reproduce with full logging enabled

3. **Reproduce Consistently**
   - Can you trigger it reliably?
   - What are the exact steps?
   - Does it happen every time?
   - If not reproducible → ask the user for more context; don't guess

4. **Gather Evidence in Multi-Component Systems**

   **WHEN system has multiple components (CI → build → signing, API → service → database):**

   **BEFORE proposing fixes, add diagnostic instrumentation:**
   ```
   For EACH component boundary:
     - Log what data enters component
     - Log what data exits component
     - Verify environment/config propagation
     - Check state at each layer

   Run once to gather evidence showing WHERE it breaks
   THEN analyze evidence to identify failing component
   THEN investigate that specific component
   ```

   **Example (multi-layer system):**
   ```bash
   # Layer 1: Workflow
   echo "=== Secrets available in workflow: ==="
   echo "IDENTITY: ${IDENTITY:+SET}${IDENTITY:-UNSET}"

   # Layer 2: Build script
   echo "=== Env vars in build script: ==="
   env | grep IDENTITY || echo "IDENTITY not in environment"

   # Layer 3: Signing script
   echo "=== Keychain state: ==="
   security list-keychains
   security find-identity -v

   # Layer 4: Actual signing
   codesign --sign "$IDENTITY" --verbose=4 "$APP"
   ```

   **This reveals:** Which layer fails (secrets → workflow ✓, workflow → build ✗)

5. **Trace Data Flow**

   **WHEN error is deep in call stack:**

   See `root-cause-tracing.md` in this directory for the complete backward tracing technique.

   **Quick version:**
   - Where does bad value originate?
   - What called this with bad value?
   - Keep tracing up until you find the source
   - Fix at source, not at symptom

6. **Document Multiple Possible Causes → Troubleshooting.md**

   When investigation reveals 2 or more possible root causes, create a **Troubleshooting.md** before continuing (see dedicated section below). Keep it updated throughout the investigation.

---

### Phase 2: Pattern Analysis

**Find the pattern before fixing:**

1. **Find Working Examples**
   - Locate similar working code in same codebase
   - What works that's similar to what's broken?

2. **Compare Against References**
   - If implementing pattern, read reference implementation COMPLETELY
   - Don't skim - read every line
   - Understand the pattern fully before applying

3. **Identify Differences**
   - What's different between working and broken?
   - List every difference, however small
   - Don't assume "that can't matter" — ask the user if significance is unclear

4. **Understand Dependencies**
   - What other components does this need?
   - What settings, config, environment?
   - What assumptions does it make?

---

### Phase 3: Hypothesis and Testing

**Scientific method:**

1. **Form Single Hypothesis**
   - State clearly: "I think X is the root cause because Y"
   - Write it to Troubleshooting.md (_if it exists_)
   - Be specific, not vague

2. **Test Minimally**
   - Make the SMALLEST possible change to test hypothesis
   - One variable at a time
   - Don't fix multiple things at once

3. **Verify Before Continuing**
   - Did it work? Yes → Phase 4
   - Didn't work? Form NEW hypothesis
   - DON'T add more fixes on top

4. **When You Don't Know**
   - Say "I don't understand X"
   - Don't pretend to know
   - Ask the user — they may have context you don't
   - Research more before guessing

---

### Phase 4: Implementation

**Fix the root cause, not the symptom:**

1. **Create Failing Test Case**
   - Simplest possible reproduction
   - Automated test if possible
   - One-off test script if no framework
   - MUST have before fixing
   - Use the `superpowers:test-driven-development` skill for writing proper failing tests

2. **Implement Single Fix**
   - Address the root cause identified
   - ONE change at a time
   - No "while I'm here" improvements
   - No bundled refactoring

3. **Verify Fix**
   - Test passes now?
   - No other tests broken?
   - Issue actually resolved?

4. **If Fix Doesn't Work**
   - STOP
   - Count: How many fixes have you tried?
   - If < 3: Return to Phase 1, re-analyze with new information
   - **If ≥ 3: STOP and question the architecture (step 5 below)**
   - DON'T attempt Fix #4 without architectural discussion

5. **If 3+ Fixes Failed: Question Architecture**

   **Pattern indicating architectural problem:**
   - Each fix reveals new shared state/coupling/problem in different place
   - Fixes require "massive refactoring" to implement
   - Each fix creates new symptoms elsewhere

   **STOP and question fundamentals:**
   - Is this pattern fundamentally sound?
   - Are we "sticking with it through sheer inertia"?
   - Should we refactor architecture vs. continue fixing symptoms?

   **Discuss with your human partner before attempting more fixes**

   This is NOT a failed hypothesis - this is a wrong architecture.

---

## Troubleshooting.md

When investigation surfaces 2 or more possible root causes, create a **Troubleshooting.md** to track them.

### Location

| Branch type | Location |
|---|---|
| Speckit feature branch (`NNN-word1-word2-word3` or `feature/NNN-word1-word2-word3`) | `specs/<branch-name>/Troubleshooting.md` |
| Any other branch or context | Project root `Troubleshooting.md` |

Example: branch `feature/001-library-view-enhancements` → `specs/001-library-view-enhancements/Troubleshooting.md`

### Required Content

```markdown
# Troubleshooting: <short bug description>

## Symptom
<What the user reported, verbatim if possible>

## Assumptions Confirmed with User
- <assumption 1> → confirmed / refuted
- <assumption 2> → confirmed / refuted

## Branch Changes Under Investigation
- <list of modified files / changes from git diff main>

## Possible Causes (ordered by likelihood)

### 1. <Most likely cause>
**Status**: OPEN | DISPROVEN | CONFIRMED
**Reasoning**: <why this is a candidate>
**Evidence for**: <anything supporting it>
**Evidence against**: <anything ruling it out>
**Verdict**: <if DISPROVEN: explanation of how it was ruled out — keep this entry>

### 2. <Second cause>
...

## Knowledge Gained
<Add new findings here continuously so another agent can pick up where this one left off>
```

### Rules
- Order possible causes by likelihood (most likely first)
- **Never delete a disproven cause** — mark it DISPROVEN and explain why it was ruled out. This prevents re-investigating the same dead ends.
- Update this file after every significant finding — it is the handoff document
- If investigation is handed off to another agent, this file is the source of truth

---

## Red Flags - STOP and Follow Process

If you catch yourself thinking:
- "Quick fix for now, investigate later"
- "Just try changing X and see if it works"
- "Add multiple changes, run tests"
- "Skip the test, I'll manually verify"
- "It's probably X, let me fix that"
- "I don't fully understand but this might work"
- "Pattern says X but I'll adapt it differently"
- "Here are the main problems: [lists fixes without investigation]"
- Proposing solutions before tracing data flow
- **"One more fix attempt" (when already tried 2+)**
- **Each fix reveals new problem in different place**
- Starting code analysis without first checking branch changes (`git diff main`)
- Formed an assumption without writing it down and asking the user
- Starting investigation on an incomplete or ambiguous bug description
- Answering "I don't know which component is involved" without asking the user

**ALL of these mean: STOP. Return to the appropriate phase.**

**If 3+ fixes failed:** Question the architecture (see Phase 4.5)

## Your Human Partner's Signals You're Doing It Wrong

**Watch for these redirections:**
- "Is that not happening?" - You assumed without verifying
- "Will it show us...?" - You should have added evidence gathering
- "Stop guessing" - You're proposing fixes without understanding
- "Ultrathink this" - Question fundamentals, not just symptoms
- "We're stuck?" (frustrated) - Your approach isn't working
- "Didn't you ask about that?" - You acted on an assumption instead of asking

**When you see these:** STOP. Return to Phase 0 or Phase 1.

## Common Rationalizations

| Excuse | Reality |
|--------|---------|
| "Issue is simple, don't need process" | Simple issues have root causes too. Process is fast for simple bugs. |
| "Emergency, no time for process" | Systematic debugging is FASTER than guess-and-check thrashing. |
| "Just try this first, then investigate" | First fix sets the pattern. Do it right from the start. |
| "I'll write test after confirming fix works" | Untested fixes don't stick. Test first proves it. |
| "Multiple fixes at once saves time" | Can't isolate what worked. Causes new bugs. |
| "Reference too long, I'll adapt the pattern" | Partial understanding guarantees bugs. Read it completely. |
| "I see the problem, let me fix it" | Seeing symptoms ≠ understanding root cause. |
| "One more fix attempt" (after 2+ failures) | 3+ failures = architectural problem. Question pattern, don't fix again. |
| "I can figure this out from the code" | The user has context you don't. Ask early, not after thrashing. |
| "The bug description is good enough" | Ambiguous requirements → wrong root cause. Clarify first. |
| "It's probably from before this branch" | Assume your branch caused it unless told otherwise. Check git diff first. |

## Quick Reference

| Phase | Key Activities | Success Criteria |
|-------|---------------|------------------|
| **0. Requirements** | Confirm bug description is complete; ask if not | Clear, unambiguous bug statement |
| **1. Root Cause** | Check branch diff; read errors; note & ask about assumptions; gather evidence | Understand WHAT and WHY |
| **2. Pattern** | Find working examples, compare | Identify differences |
| **3. Hypothesis** | Form theory, test minimally | Confirmed or new hypothesis |
| **4. Implementation** | Create test, fix, verify | Bug resolved, tests pass |

## When Process Reveals "No Root Cause"

If systematic investigation reveals issue is truly environmental, timing-dependent, or external:

1. You've completed the process
2. Document what you investigated
3. Implement appropriate handling (retry, timeout, error message)
4. Add monitoring/logging for future investigation

**But:** 95% of "no root cause" cases are incomplete investigation.

## Supporting Techniques

These techniques are part of systematic debugging and available in this directory:

- **`root-cause-tracing.md`** - Trace bugs backward through call stack to find original trigger
- **`defense-in-depth.md`** - Add validation at multiple layers after finding root cause
- **`condition-based-waiting.md`** - Replace arbitrary timeouts with condition polling

**Related skills:**
- **superpowers:test-driven-development** - For creating failing test case (Phase 4, Step 1)
- **superpowers:verification-before-completion** - Verify fix worked before claiming success

## Real-World Impact

From debugging sessions:
- Systematic approach: 15-30 minutes to fix
- Random fixes approach: 2-3 hours of thrashing
- First-time fix rate: 95% vs 40%
- New bugs introduced: Near zero vs common
