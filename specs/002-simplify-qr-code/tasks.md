# Tasks: Simplify QR Code by Removing SessionId from Token

**Input**: Design documents from `specs/main/`
**Plan**: [plan.md](plan.md) | **Research**: [research.md](research.md) | **Data model**: [data-model.md](data-model.md) | **Contract**: [contracts/ITokenService.md](contracts/ITokenService.md)
**Scope**: Backend-only refactoring — no spec.md user stories; tasks follow plan steps

## Format: `[ID] [P?] Description — file path`

- **[P]**: Can run in parallel with other [P] tasks at the same dependency level (different files)
- No `[Story]` labels — this plan has no user stories from spec.md; tasks are organized by plan step
- Include exact file paths in all descriptions

---

## Phase 1: Setup

**Purpose**: Create feature branch and confirm a clean baseline before any changes.

- [ ] T001 Create feature branch `feature/simplify-qr-token` (never commit to `main`): `git checkout -b feature/simplify-qr-token`
- [ ] T002 Confirm clean baseline — `dotnet build` must produce zero errors/warnings; `dotnet test Karamel.Backend.Tests -v minimal` must pass

---

## Phase 2: Foundational — Update `ITokenService` Interface

**Purpose**: Change the `ValidateLinkToken` signature first. This is the single blocking change — all callers and the implementation must be updated atomically before the solution will compile.

**⚠️ CRITICAL**: The compiler will break after T003 until T004–T007 are complete. Work through Phase 2 and Phase 3 without running the build in between until T007.

- [ ] T003 Update `ValidateLinkToken` signature in `Karamel.Backend/Services/ITokenService.cs` — change from `(Guid sessionId, string role, bool isValid) ValidateLinkToken(string token)` to `(string role, bool isValid) ValidateLinkToken(string token, Guid sessionId)`; update XML doc comment

**Checkpoint**: Interface updated — implementation and callers must now be fixed before the build will succeed

---

## Phase 3: Core Implementation — `TokenService` and Filters

**Purpose**: Update the implementation and both filter callers to match the new interface.

### Step 2 — `TokenService` implementation (two edits, sequential in same file)

- [ ] T004 Update `GenerateLinkToken` in `Karamel.Backend/Services/TokenService.cs` — change payload from `{sessionId}|{role}|{hmac}` to `{role}|{hmac}`; change HMAC input from `$"{sessionId}|{role}"` to `$"{sessionId}:{role}"`; update XML doc comment
- [ ] T005 Update `ValidateLinkToken` in `Karamel.Backend/Services/TokenService.cs` — add `Guid sessionId` parameter; parse token as 2-part `{role}|{hmac}` (reject if `parts.Length != 2`); verify HMAC using `ComputeHmac($"{sessionId}:{role}")`; return `(string role, bool isValid)` tuple; keep `AreEqualConstantTime` and `ComputeHmac` helpers untouched

### Steps 3 & 4 — Filter callers (parallel, different files)

- [ ] T006 [P] Update `LinkTokenActionFilter` in `Karamel.Backend/Filters/LinkTokenActionFilter.cs` — replace validation call with `var (_, isValid) = tokenService.ValidateLinkToken(token, sessionId);`; remove `tokenSessionId != sessionId` check; remove `expectedToken` generation block; simplify logging to single masked `ReceivedLength`+`ReceivedPrefix` log line (see plan.md Step 3)
- [ ] T007 [P] Update `ValidateTokenAndExtractRole` in `Karamel.Backend/Filters/LinkTokenHubFilter.cs` — replace `var (tokenSessionId, role, isValid) = _tokenService.ValidateLinkToken(token);` with `var (role, isValid) = _tokenService.ValidateLinkToken(token, sessionId);`; remove `tokenSessionId != sessionId` check; all other methods unchanged

**Checkpoint**: `dotnet build` must now produce zero errors and zero warnings before proceeding

- [ ] T008 Run `dotnet build` — resolve every compiler error and warning before proceeding to tests

---

## Phase 4: Tests — `TokenServiceTests`

**Purpose**: Update all existing `ValidateLinkToken` call sites in tests and add the two new cross-session / backward-compat regression tests specified in the plan.

### Update existing tests (sequential — same file)

- [ ] T009 Update `ValidateLinkToken_AcceptsValidToken` in `Karamel.Backend.Tests/TokenServiceTests.cs` — call `ValidateLinkToken(token, sessionId)`; destructure `(_, isValid)`; remove `Assert.Equal(sessionId, tokenSessionId)` assertion
- [ ] T010 Update `ValidateLinkToken_RejectsInvalidToken` in `Karamel.Backend.Tests/TokenServiceTests.cs` — call `ValidateLinkToken("invalid-token", Guid.NewGuid())`; destructure `(_, isValid)`
- [ ] T011 Update `ValidateLinkToken_RejectsNullOrEmptyToken` in `Karamel.Backend.Tests/TokenServiceTests.cs` — pass `Guid.NewGuid()` as second arg; destructure `(_, isValid1)` / `(_, isValid2)`
- [ ] T012 Update `ValidateLinkToken_RejectsTokenForDifferentSession` in `Karamel.Backend.Tests/TokenServiceTests.cs` — generate token for `sessionId1`, validate with `sessionId2` → expect `isValid=false`; remove assertions that checked the returned `tokenSessionId`
- [ ] T013 Update `GenerateLinkToken_ProducesExpectedLength` in `Karamel.Backend.Tests/TokenServiceTests.cs` — change bounds to `> 60` and `< 120` (new token encodes to ~68 chars); update comment
- [ ] T014 Update `ValidateLinkToken_RejectsStandardBase64Token` in `Karamel.Backend.Tests/TokenServiceTests.cs` — call `ValidateLinkToken(standardBase64Token, Guid.NewGuid())`; destructure `(_, isValid)`
- [ ] T015 Update `ValidateLinkToken_WithValidAdminToken_ReturnsAdminRole` in `Karamel.Backend.Tests/TokenServiceTests.cs` — call `ValidateLinkToken(token, sessionId)`; destructure `(role, isValid)`; remove `Assert.Equal(sessionId, returnedSessionId)`
- [ ] T016 Update `ValidateLinkToken_WithValidSingerToken_ReturnsSingerRole` in `Karamel.Backend.Tests/TokenServiceTests.cs` — same pattern as T015
- [ ] T017 Update `ValidateLinkToken_WithTamperedRole_ReturnsFalse` in `Karamel.Backend.Tests/TokenServiceTests.cs` — decode as 2-part token `{role}|{hmac}`, swap role to `"singer"`, re-encode, validate with correct `sessionId` → `isValid=false`; destructure `(role, isValid)`
- [ ] T018 Update `ValidateLinkToken_WithInvalidToken_ReturnsFalse` in `Karamel.Backend.Tests/TokenServiceTests.cs` — call `ValidateLinkToken("invalid-token", Guid.NewGuid())`; destructure `(role, isValid)`
- [ ] T019 Update `ValidateLinkToken_WithNullToken_ReturnsFalse` in `Karamel.Backend.Tests/TokenServiceTests.cs` — call `ValidateLinkToken(null!, Guid.NewGuid())`; destructure `(role, isValid)`
- [ ] T020 Update `GenerateLinkToken_DefaultRole_IsAdmin` in `Karamel.Backend.Tests/TokenServiceTests.cs` — call `ValidateLinkToken(token, sessionId)`; destructure `(role, isValid)`

### Add new regression tests

- [ ] T021 Add `ValidateLinkToken_WithWrongSessionId_ReturnsFalse` to `Karamel.Backend.Tests/TokenServiceTests.cs` — generate token for `sessionId1`; call `ValidateLinkToken(token, sessionId2)`; assert `isValid=false` and `role=""` (verifies cross-session replay protection)
- [ ] T022 Add `ValidateLinkToken_OldThreePartFormat_IsRejected` to `Karamel.Backend.Tests/TokenServiceTests.cs` — construct `Base64url({sessionId}|admin|fakehmacsignature…)` manually; call `ValidateLinkToken(oldToken, sessionId)`; assert `isValid=false` (verifies backward-compat token is rejected cleanly)

**Checkpoint**: Run targeted test file — MUST pass before running full suite

- [ ] T023 Run `dotnet test Karamel.Backend.Tests --filter "FullyQualifiedName~TokenServiceTests" -v minimal` — all token tests must pass; fix any failures before continuing

---

## Phase 5: Integration Verification — `PlaylistHubTests`

**Purpose**: Confirm that integration tests pass without any changes to `PlaylistHubTests.cs`. Tokens in integration tests flow from the live `/api/sessions` endpoint, so they automatically use the new format.

- [ ] T024 Run `dotnet test Karamel.Backend.Tests -v minimal` (full backend suite including `PlaylistHubTests`) — all tests must pass; no new skips introduced
- [ ] T025 If any `PlaylistHubTests` failure occurs: search file for direct token-format assertions (grep `linkToken`); fix only if a test asserts on internal token structure (currently none do per research.md)

---

## Phase 6: Polish & Cross-Cutting Verification

**Purpose**: Full-solution quality gates and manual QR code measurement.

- [ ] T026 Run `dotnet build` (full solution) — zero errors, zero warnings
- [ ] T027 [P] Run frontend JS tests sanity check — `cd Karamel.Web\wwwroot; npm run test:run; cd ..\..` — zero failures expected (no frontend changes made)
- [ ] T028 Manual verification: `dotnet run --project Karamel.Web` → create session → open NextSongView → measure QR URL length in browser DevTools — confirm ≈ 50 chars shorter than previous baseline (~217-247 → ~167-197)
- [ ] T029 Manual verification: scan QR code with mobile device → SingerView loads → singer can add a song → song appears in playlist on admin Playlist page
- [ ] T030 Manual verification: admin controls still work (clear queue, change song status)
- [ ] T031 Commit on `feature/simplify-qr-token` branch with message `Remove redundant sessionId from link token to reduce QR code URL length`

---

## Dependencies & Execution Order

### Phase Dependencies

```
Phase 1 (Setup)
  └── Phase 2 (ITokenService interface) — BLOCKS everything
        └── Phase 3 (TokenService + Filters) — BLOCKS tests
              ├── Phase 4 (TokenServiceTests) — unit tests
              └── Phase 5 (PlaylistHubTests) — depends on Phase 4 passing
                    └── Phase 6 (Polish) — final gates
```

### Parallel Opportunities

**Within Phase 3** (after T003, T005 complete — both compile):
- T006 (ActionFilter) ∥ T007 (HubFilter) — different files, no shared state

**Within Phase 6**:
- T026 (build) ∥ T027 (JS tests) — independent toolchains

### Task Run Commands

```powershell
# Targeted: TokenService unit tests only (run after T023)
dotnet test Karamel.Backend.Tests --filter "FullyQualifiedName~TokenServiceTests" -v minimal

# Full backend suite (Phase 5)
dotnet test Karamel.Backend.Tests -v minimal

# JS tests (Phase 6 sanity check)
cd Karamel.Web\wwwroot; npm run test:run; cd ..\..
```

---

## Implementation Strategy

**MVP scope**: All tasks are required — this is a focused 6-step refactoring with no optional increments. The entire change is backend-only and must be applied atomically (interface + implementation + filters + tests together) to avoid a partial broken state.

**Estimated task count**: 31 tasks total  
- Phase 1 (Setup): 2  
- Phase 2 (Interface): 1 blocking task  
- Phase 3 (Implementation): 4 (2 sequential + 2 parallel)  
- Phase 4 (Unit Tests): 14 updated + 2 new + 1 checkpoint  
- Phase 5 (Integration): 2  
- Phase 6 (Polish): 6  

**Key risk**: T003 breaks the build until T004–T007 all complete. Work through Phase 3 in a single session without interrupting between interface and its callers.
