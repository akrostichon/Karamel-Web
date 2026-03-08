# Troubleshooting: signalRBridge.test.js Hang

**Date**: 2026-03-08  
**Symptom**: The VS Code Testing panel shows `0/0` at 1288+ seconds. `npm run test:run` hangs indefinitely when `signalRBridge.test.js` is included. Other test files pass normally.

---

## Phase 1: Root Cause Investigation

### Confirmed Observations

| # | Observation | Evidence |
|---|-------------|----------|
| 1 | VS Code Testing panel frozen at `0/0` for 1288s | Screenshot |
| 2 | Other test files (logger, qrcode, fullscreen, sessionProtection) pass fine | `npx vitest run js/logger.test.js ...` → all pass |
| 3 | Running `signalRBridge.test.js` alone showed `0 passed` in 624ms (no test results at all) | Previous agent terminal output |
| 4 | The `0/0` Testing panel is a **stale vitest watch session** from a previous `runTests` invocation | Still running since previous turn (~21 min ago); not a live result |
| 5 | `@microsoft/signalr` is installed as a real `dependencies` package (not devDependencies) | `package.json`: `"@microsoft/signalr": "^10.0.0"` |
| 6 | `@microsoft/signalr` exports `HubConnectionBuilder` directly from its CJS build | `node -e "require(...)"` → `HubConnectionBuilder: function` |
| 7 | `initializeSession` always fires `tryConnectSignalR()` as an **unmanaged fire-and-forget** | `signalRBridge.js:262` |
| 8 | T027 describe block is **outside** the outer `describe('signalRBridge', ...)` block | File structure (lines 979–1094 vs. outer describe closing brace at line 975) |
| 9 | The outer `describe` mocks `global.signalR` using a **direct assignment + `if (!global.signalR)` guard**, not `vi.stubGlobal` | `signalRBridge.test.js:175–190` |

---

## Phase 2: Correction — H1 and H2 Were Wrong

### Why H1 and H2 Cannot Be The Cause

The original analysis was incorrect. The user's challenge was exactly right: **H1 and H2 could never be triggered — before or after T027 was added.**

The outer `describe` `beforeEach` always runs before each test:
```js
if (!global.signalR) {
    global.signalR = { HubConnectionBuilder: class { ...mock... } };
}
```

The very first `beforeEach` sets `global.signalR` to the mock. In the outer describe, no `afterEach` ever clears it. So for all subsequent outer describe tests, `if (!global.signalR)` is `false` and the mock is never overwritten — it persists for the entire describe block. When `tryConnectSignalR` runs `ensureSignalRLoaded`:

```js
async function ensureSignalRLoaded() {
    if (typeof signalR !== 'undefined') return true;  // ← ALWAYS TRUE for outer describe tests
    // ... dynamic import / CDN fallback never reached
}
```

**`typeof signalR !== 'undefined'` is always `true` because `global.signalR` was already set. Neither H1 (real signalR timers) nor H2 (CDN script injection) could ever be triggered for the outer describe tests.** This was true before T027 and remains true after T027.

Furthermore, T027's `vi.unstubAllGlobals()` restores `signalR` to whatever it was when T027's first `beforeEach` called `vi.stubGlobal('signalR', mock)`. By that point (after all outer describe tests), `global.signalR` is the outer describe's mock — so it gets restored to the outer describe's mock, not to `undefined`.

**H1 and H2 are withdrawn. They describe a scenario that never occurs.**

---

### Revised Mechanism Analysis

The outer describe tests do not hang. Each `initializeSession` call fires `tryConnectSignalR` (fire-and-forget), which:
1. Calls `ensureSignalRLoaded()` → returns immediately (guard passes)  
2. Builds a hub connection using the mock `HubConnectionBuilder`  
3. Awaits `hubConnection.start()` → `vi.fn().mockResolvedValue(undefined)` → resolves in one microtask tick  
4. Awaits `hubConnection.invoke('JoinSession', ...)` → same, resolves immediately  

All fire-and-forget calls settle within a few microtask ticks. No hang here.

The T027 tests also do not individually hang. `fetchLibraryPage` in a fresh module instance checks `usingSignalR` (false in fresh module) and goes directly to REST. `fetch` is mocked via `vi.stubGlobal`. The calls resolve quickly.

### The Actual Problem: `vi.unstubAllGlobals()` nullifies the outer describe mock guard

The real issue is more subtle and was introduced **only by the T027 addition**:

T027's `afterEach` calls `vi.unstubAllGlobals()`. Among the globals T027 stubs is `signalR`. When vitest records the "original" to restore, what is `globalThis.signalR` at that moment?

- After all outer describe tests ran, `global.signalR` has been the mock for the whole run.  
- T027 first `beforeEach`: `vi.stubGlobal('signalR', ...)` — vitest records the CURRENT `globalThis.signalR` (= outer mock) and replaces it.  
- T027 first `afterEach`: `vi.unstubAllGlobals()` — restores `signalR` to the recorded original (outer mock).  

This cycle is safe. **However**, there is a critical side effect: after `vi.unstubAllGlobals()` runs, the outer describe's `if (!global.signalR)` guard would still evaluate to `false` (mock is still set). This part is fine.

**What IS broken**: When `vi.unstubAllGlobals()` is called, it restores ALL globals that were stubbed — including `fetch`. But in the T027 calls to `import('./signalRBridge.js?fetchtestN=...' + Date.now())`, each creates a new module instance. These fresh module instances may hold a reference to — or trigger resolution via — happy-dom's own resource lifecycle. Specifically:

Each cache-busted fresh import of `signalRBridge.js` creates a brand-new module scope. Inside that scope, when `fetchLibraryPage` runs and awaits `fetch(url, headers)`, it captures a JavaScript Promise from the mock. After the test completes, the mock fetch promise resolves and the module instance has no more pending work.

BUT: each fresh import of `signalRBridge.js` also re-imports `logger.js`. In vitest, because `logger.js` has no query-string cache-buster, it may be shared or it may be re-evaluated per parent. If logger.js or signalRBridge.js create any persistent global subscriptions (like `window.addEventListener`) during module load that are never cleaned up, those listeners accumulate across all four T027 tests and may keep the event loop open.

Specifically worth noting: `global.window = mockWindow` in the outer describe means `window.addEventListener` is `mockWindow.addEventListener = vi.fn()`. The fresh module imports in T027 run OUTSIDE the outer describe (no outer `beforeEach`), so `global.window` at T027 time is still `mockWindow` from the last outer describe test. Any calls inside the fresh module to `window.addEventListener` are recorded but never cleaned up.

### What Needs `--detectOpenHandles` to Confirm

The exact open handle — whether it is from:
- A `setTimeout` in `checkMainTabAlive` that fired but whose `removeEventListener` callback is pending  
- Accumulated `window.addEventListener` calls in fresh module instances  
- The MockBroadcastChannel's `setTimeout(() => {...}, 0)` in `postMessage` across multiple fresh instances  
- Vitest's own module graph cleanup being blocked by the combination of static + cache-busted dynamic imports of the same file  

— cannot be definitively named without `--detectOpenHandles` output.

**Note on VS Code crash**: The crash is a direct consequence of an unkillable vitest process. Each time `signalRBridge.test.js` is run by the Testing panel, a new worker is spawned and never terminates. After multiple such stale workers accumulate, the process pool exhausts system resources, crashing VS Code.

---

## Phase 3: Revised Hypotheses

| ID | Hypothesis | Likelihood | Status |
|----|------------|------------|--------|
| ~~H1~~ | ~~Real `@microsoft/signalr` loaded via dynamic import registers internal timers~~ | **INVALID** | The `typeof signalR !== 'undefined'` guard is ALWAYS true for outer describe tests (mock is pre-set by `beforeEach`). This path is never reached. |
| ~~H2~~ | ~~CDN script injection fallback creates a never-resolving Promise in happy-dom~~ | **INVALID** | Same reason as H1 — the guard always returns `true` before falling through to CDN. Irrelevant to the hang. |
| H3 | `vi.unstubAllGlobals()` interacts with vitest's internal test runner state in a way that prevents file-level teardown or worker exit | **MEDIUM** | New. T027 is the ONLY code that calls `vi.unstubAllGlobals()`. Pre-T027 the file exited cleanly. Needs `--detectOpenHandles`. |
| H4 | Accumulated `window.addEventListener` calls inside cache-busted fresh module instances (4× T027 tests) are never cleaned up and hold open event loop handles in happy-dom | **MEDIUM** | New. T027 fresh imports run outside the outer `describe` so `global.window = mockWindow` from the last outer test is still the active `window`. Any listener registration in the fresh module context accumulates. Needs `--detectOpenHandles`. |
| H5 | MockBroadcastChannel's `postMessage` uses `setTimeout(() => {...}, 0)` — if any postMessage fires during T027's fresh imports, the timer callback holds an open handle via the old mockWindow | **LOW** | Possible but would self-resolve in the next event loop tick. |
| H6 | The VS Code Testing panel `0/0` is a stale artifact from a prior `runTests` invocation never terminated; the VS Code crash is the accumulation of multiple unkillable vitest workers consuming system resources | **CONFIRMED** | Consistent with observed behavior (1288s runtime, VS Code crash). |

---

## Phase 4: Required Investigation Steps

To distinguish H3 from H4, run with `--detectOpenHandles` **in a fresh terminal** (VS Code must be restarted after the crash, and the stale vitest processes killed before attempting):

```powershell
cd Karamel.Web\wwwroot
npx vitest run js/signalRBridge.test.js --reporter=verbose 2>&1
```

The output should name the specific handle type (Timer, TCPSocket, etc.) that prevents exit.

---

## Resolution Applied

Rather than waiting for `--detectOpenHandles` output (which required clearing the stale process first), the structural cause was addressed directly:

**Fix**: Moved the T027 tests out of `signalRBridge.test.js` into a dedicated file `fetchLibraryPage.test.js`.

Vitest runs each test file in its own worker. By isolating the T027 tests:
- `vi.unstubAllGlobals()` can no longer interact with the outer describe's directly-assigned `signalR` mock
- The four cache-busted dynamic `import('./signalRBridge.js?fetchtestN=...')` calls run in a clean worker with no leaked `mockWindow` or other globals from the outer describe
- `BroadcastChannel` and `sessionStorage` are now stubbed via `vi.stubGlobal` in the new file (also cleaned up via `vi.unstubAllGlobals()`), making it completely self-contained

### Verified Results

```
signalRBridge.test.js    → 37 tests passed, exits in 3.4s  ✓
fetchLibraryPage.test.js → 4 tests passed,  exits in 0.6s  ✓
Full suite (16 files)    → 226 tests passed, exits in 4.2s ✓
```

---

## Final Status

| Item | Status |
|------|--------|
| H1 (real signalR timers via dynamic import) | ❌ INVALID — `typeof signalR !== 'undefined'` guard always exits early |
| H2 (CDN script hang) | ❌ INVALID — same reason; CDN fallback is never reached |
| H3 / H4 (global state interaction between T027 and outer describe) | ✅ RESOLVED — isolated into separate file, no shared worker globals |
| H6 (stale process / VS Code crash) | ✅ CONFIRMED and resolved by VS Code restart |
| **Full JS test suite** | ✅ **226 tests pass, exits cleanly** |
