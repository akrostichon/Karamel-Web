---
description: "Task list for removing deprecated methods and properties"
---

# Tasks: Remove Deprecated Code (LinkToken, BroadcastPlaylistUpdatedAsync)

**Input**: Design documents from `/specs/003-remove-deprecated-code/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: No new tests required - existing test suite validates zero regressions

**Organization**: Tasks are grouped by user story (P1, P2, P3) to enable incremental implementation and testing.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1=P1, US2=P2, US3=P3)
- Include exact file paths in descriptions

---

## Phase 1: Setup

**Purpose**: Verify branch and baseline

- [X] T001 Verify current branch is `003-remove-deprecated-code` (or create it from main)
- [X] T002 Run `dotnet build` and ensure zero errors/warnings (baseline check)
- [X] T003 [P] Run `dotnet test Karamel.Web.Tests` and ensure ≥251 passing, 9 skipped (baseline)
- [X] T004 [P] Run `npm run test:run` in Karamel.Web/wwwroot and ensure ≥222 tests pass (baseline)

---

## Phase 2: Foundational

**Purpose**: No foundational tasks required - this is a pure cleanup feature with no infrastructure changes

**⚠️ CRITICAL**: This phase is intentionally empty. All work is organized by user story priority (P1 → P2 → P3).

**Checkpoint**: Proceed directly to User Story 1 (P1)

---

## Phase 3: User Story 1 - Remove BroadcastPlaylistUpdatedAsync (Priority: P1) 🎯 MVP

**Goal**: Remove no-op method from SignalR bridge interface and implementation

**Independent Test**: Build succeeds, all tests pass, no call sites remain

### Implementation for User Story 1

- [X] T005 [US1] Search for all call sites to `BroadcastPlaylistUpdatedAsync` in Karamel.Web/Store/Playlist/PlaylistEffects.cs
- [X] T006 [US1] Remove all `await _signalRBridge.BroadcastPlaylistUpdatedAsync();` invocations from PlaylistEffects.cs (typically in AddSongToPlaylistEffect, RemoveItemEffect, ReorderPlaylistEffect)
- [X] T007 [P] [US1] Remove `Task BroadcastPlaylistUpdatedAsync();` method declaration from Karamel.Web/Services/ISignalRPlaylistBridge.cs
- [X] T008 [P] [US1] Remove `public Task BroadcastPlaylistUpdatedAsync() => Task.CompletedTask;` implementation from Karamel.Web/Services/SignalRPlaylistBridge.cs
- [X] T009 [US1] Build solution with `dotnet build` and verify zero errors/warnings
- [X] T010 [US1] Run `dotnet test Karamel.Web.Tests` and verify ≥251 passing, 9 skipped
- [X] T011 [US1] Search codebase for "BroadcastPlaylist" and verify zero results (except this tasks.md)
- [X] T012 [US1] Commit changes: "Remove deprecated BroadcastPlaylistUpdatedAsync no-op method"

**Checkpoint**: User Story 1 complete - no-op method fully removed

---

## Phase 4: User Story 2 - Remove LinkToken Backend (Priority: P2)

**Goal**: Remove LinkToken property, database column, repository/service methods, and hub filter

**Independent Test**: Create session via API, verify response contains adminToken/singerToken but NOT linkToken

### Implementation for User Story 2

#### Remove LinkTokenHubFilter and Registration

- [ ] T013 [P] [US2] Delete entire file Karamel.Backend/Filters/LinkTokenHubFilter.cs
- [ ] T014 [US2] Remove `options.AddFilter<LinkTokenHubFilter>();` (or similar) from Karamel.Backend/Program.cs hub configuration

#### Remove ITokenService Methods

- [ ] T015 [P] [US2] Remove `string GenerateLinkToken(Guid sessionId);` method declaration from Karamel.Backend/Services/ITokenService.cs
- [ ] T016 [P] [US2] Remove `bool ValidateLinkToken(Guid sessionId, string token);` method declaration from Karamel.Backend/Services/ITokenService.cs
- [ ] T017 [US2] Remove implementations of `GenerateLinkToken` and `ValidateLinkToken` from Karamel.Backend/Services/TokenService.cs

#### Remove ISessionRepository Method

- [ ] T018 [P] [US2] Remove `Task<Session?> GetByLinkTokenAsync(string linkToken);` method declaration from Karamel.Backend/Repositories/ISessionRepository.cs
- [ ] T019 [US2] Remove implementation of `GetByLinkTokenAsync` from Karamel.Backend/Repositories/SessionRepository.cs

#### Update SessionsController

- [ ] T020 [US2] In Karamel.Backend/Controllers/SessionsController.cs Create method, remove `LinkToken = _tokenService.GenerateLinkToken(session.Id)` assignment
- [ ] T021 [US2] In SessionsController.cs Create method response, remove `linkToken = session.LinkToken` field from anonymous object

#### Remove Session Model Property

- [ ] T022 [US2] Remove `public string? LinkToken { get; set; }` property from Karamel.Backend/Models/Session.cs

#### Create and Apply Database Migration

- [ ] T023 [US2] Generate EF Core migration: `dotnet ef migrations add RemoveLinkToken --project Karamel.Backend`
- [ ] T024 [US2] Edit generated migration file Up() method to add data migration: `migrationBuilder.Sql("UPDATE Sessions SET AdminToken = LinkToken WHERE AdminToken IS NULL AND LinkToken IS NOT NULL");` before DropColumn
- [ ] T025 [US2] Edit generated migration file Down() method to add data migration: `migrationBuilder.Sql("UPDATE Sessions SET LinkToken = AdminToken");` after AddColumn
- [ ] T026 [US2] Apply migration to local SQLite database: `dotnet ef database update --project Karamel.Backend`

#### Update Log Messages

- [ ] T027 [US2] Search Karamel.Backend for log messages containing "LinkToken" and update to use "AdminToken" instead

#### Verification

- [ ] T028 [US2] Build solution with `dotnet build` and verify zero errors/warnings
- [ ] T029 [US2] Run `dotnet test Karamel.Backend.Tests -v minimal` and verify all tests pass
- [ ] T030 [US2] Run `dotnet test Karamel.Web.Tests` and verify ≥251 passing, 9 skipped
- [ ] T031 [US2] Search Karamel.Backend for "LinkToken" and verify zero results (except in Migrations/ history)
- [ ] T032 [US2] Manual test: Start backend, POST to /api/sessions, verify response has adminToken/singerToken but NOT linkToken
- [ ] T033 [US2] Commit changes: "Remove deprecated LinkToken property and database column"

**Checkpoint**: User Story 2 complete - LinkToken fully removed from backend

---

## Phase 5: User Story 3 - Remove LinkToken Frontend Parameters (Priority: P3)

**Goal**: Remove optional `linkToken` parameters from frontend service methods

**Independent Test**: Upload library succeeds, QR code URLs contain only session and token params

### Implementation for User Story 3

#### Update ISignalRConnectionManager

- [ ] T034 [US3] Remove `string? linkToken` parameter from `InitializeAsync` method signature in Karamel.Web/Services/ISignalRConnectionManager.cs
- [ ] T035 [US3] Update `InitializeAsync` implementation in Karamel.Web/Services/SignalRConnectionManager.cs to match new signature (remove parameter)
- [ ] T036 [US3] Search for all `InitializeAsync` call sites and remove linkToken argument (typically in Karamel.Web/Pages/Home.razor)

#### Update ISessionApiClient

- [ ] T037 [US3] Remove `string? linkToken` parameter from `UploadLibraryToServerAsync` method signature in Karamel.Web/Services/ISessionApiClient.cs
- [ ] T038 [US3] Update `UploadLibraryToServerAsync` implementation in Karamel.Web/Services/SessionApiClient.cs to match new signature (remove parameter)
- [ ] T039 [US3] Search for all `UploadLibraryToServerAsync` call sites and remove linkToken argument

#### Update ISessionStorageService

- [ ] T040 [US3] Remove `string? linkToken` parameter from `GenerateSessionUrlAsync` method signature in Karamel.Web/Services/ISessionStorageService.cs
- [ ] T041 [US3] Update `GenerateSessionUrlAsync` implementation in Karamel.Web/Services/SessionStorageService.cs to match new signature (remove parameter)
- [ ] T042 [US3] Search for all `GenerateSessionUrlAsync` call sites and remove linkToken argument

#### Remove Backward Compatibility Code

- [ ] T043 [US3] In Karamel.Web/Pages/Home.razor, remove code that reads `linkToken` from session JSON (backward compatibility block)
- [ ] T044 [US3] Remove any other references to `linkToken` variable in Home.razor

#### Update XML Documentation

- [ ] T045 [P] [US3] Search Karamel.Web for XML docs (///) containing "LinkToken" or "linkToken" and update to reference "AdminToken" or remove if irrelevant

#### Verification

- [ ] T046 [US3] Build solution with `dotnet build` and verify zero errors/warnings
- [ ] T047 [US3] Run `dotnet test Karamel.Web.Tests` and verify ≥251 passing, 9 skipped
- [ ] T048 [US3] Run `npm run test:run` in Karamel.Web/wwwroot and verify ≥222 tests pass
- [ ] T049 [US3] Search Karamel.Web for "linkToken" (camelCase) and verify zero results (except this tasks.md)
- [ ] T050 [US3] Manual test: Run app, create session, verify QR code URL format is `?session={guid}&token={token}` (no linkToken)
- [ ] T051 [US3] Commit changes: "Remove deprecated linkToken parameters from frontend services"

**Checkpoint**: User Story 3 complete - linkToken fully removed from frontend

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Final verification and documentation updates

- [ ] T052 [P] Run full build: `dotnet clean; dotnet build` and verify zero errors/warnings
- [ ] T053 Run `dotnet test Karamel.Web.Tests` and verify ≥251 passing, 9 skipped (final check)
- [ ] T054 Run `dotnet test Karamel.Backend.Tests -v minimal` and verify all tests pass (final check)
- [ ] T055 [P] Run `npm run test:run` in Karamel.Web/wwwroot and verify ≥222 tests pass (final check)
- [ ] T056 Search entire solution for "LinkToken" (PascalCase) and verify zero results except in Migrations/ history
- [ ] T057 Search entire solution for "linkToken" (camelCase) and verify zero results (except this tasks.md)
- [ ] T058 Search entire solution for "BroadcastPlaylist" and verify zero results (except this tasks.md)
- [ ] T059 Verify no "DEPRECATED" comments remain in codebase (search for "DEPRECATED")
- [ ] T060 Run quickstart.md validation checklist (Success Criteria SC-001 through SC-010)
- [ ] T061 Update DEVELOPMENT_PLAN.md to mark cleanup as complete (if tracked there)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately
- **Foundational (Phase 2)**: Intentionally empty - no blocking prerequisites
- **User Story 1 (Phase 3)**: Can start after Setup baseline verification
- **User Story 2 (Phase 4)**: Can start after Setup, but recommended after US1 for incremental risk management
- **User Story 3 (Phase 5)**: Can start after Setup, but recommended after US2 to ensure backend changes are stable
- **Polish (Phase 6)**: Depends on US1, US2, US3 completion

### User Story Dependencies

- **User Story 1 (P1)**: Independent - frontend only changes
- **User Story 2 (P2)**: Independent - backend only changes (no dependencies on US1)
- **User Story 3 (P3)**: Independent - frontend only changes (no dependencies on US1 or US2)

**All three user stories can technically run in parallel**, but sequential execution (P1 → P2 → P3) is recommended for incremental testing:
1. US1 takes ~30 min, low risk, validates build/test infrastructure
2. US2 takes ~1.5 hrs, includes migration, more risk but still backend isolated
3. US3 takes ~30 min, low risk, completes frontend cleanup

### Within Each User Story

- **US1**: Task order: T005 (search) → T006 (remove calls) → T007/T008 (remove interface/impl in parallel) → T009 (build) → T010/T011 (verify) → T012 (commit)
- **US2**: Hub filter removal (T013-T014) can run in parallel with service method removals (T015-T019), then controller/model changes (T020-T022), then migration (T023-T026), then verification (T027-T033)
- **US3**: Each service interface update (T034-T036, T037-T039, T040-T042) can run independently, then backward compat removal (T043-T044), then verification (T045-T051)

### Parallel Opportunities

- **Phase 1 Setup**: T003 and T004 can run in parallel (frontend tests vs JS tests)
- **Phase 3 (US1)**: T007 and T008 can run in parallel (interface vs implementation - different files)
- **Phase 4 (US2)**: 
  - T013 and T015-T019 can run in parallel (filter deletion vs service method removals - different files)
  - T015 and T016 can run in parallel (two method declarations in same interface - but sequential is clearer)
  - T018 and T019 can run separately but T019 could be done with T018 if confident
- **Phase 5 (US3)**: 
  - T045 can start in parallel with T046-T049 (documentation vs tests - different activities)
- **Phase 6 Polish**: T052, T053, T054, T055 cannot run in parallel (build must precede tests), but T056-T059 (searches) can run in parallel

---

## Parallel Example: User Story 2 (Backend Cleanup)

```bash
# After baseline verified, launch these tasks together:
Task T013: "Delete LinkTokenHubFilter.cs"
Task T015: "Remove GenerateLinkToken from ITokenService.cs"
Task T016: "Remove ValidateLinkToken from ITokenService.cs"
Task T018: "Remove GetByLinkTokenAsync from ISessionRepository.cs"

# Then proceed sequentially:
Task T014: "Remove hub filter registration from Program.cs"
Task T017: "Remove implementations from TokenService.cs"
Task T019: "Remove implementation from SessionRepository.cs"
Task T020-T022: "Update controller and model"
Task T023-T026: "Create and apply migration"
Task T027-T033: "Verification and commit"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (verify baseline - 10 min)
2. Complete Phase 3: User Story 1 (remove no-op method - 30 min)
3. **STOP and VALIDATE**: Verify build passes, tests pass, method removed
4. Commit and consider this a successful incremental improvement

### Incremental Delivery (Recommended)

1. Complete Setup → Baseline verified
2. Complete User Story 1 (P1) → Test independently → Commit (MVP!)
3. Complete User Story 2 (P2) → Test independently → Commit (Backend cleanup done!)
4. Complete User Story 3 (P3) → Test independently → Commit (Full cleanup complete!)
5. Complete Polish → Final verification → Done

Each increment reduces code complexity without breaking existing functionality.

### Parallel Team Strategy

With multiple developers (not recommended for this feature due to small scope):

1. Team completes Setup together (10 min)
2. Split work:
   - Developer A: User Story 1 (frontend - 30 min)
   - Developer B: User Story 2 (backend - 1.5 hr)
   - Developer C: User Story 3 (frontend - 30 min, wait for US1 to avoid conflicts in Home.razor)
3. Coordinate on Home.razor if both US1 and US3 touch it (US1 unlikely to touch Home.razor, US3 will)

**Reality**: This feature is best implemented sequentially by one developer due to small scope and quick execution (~2.5-3 hours total).

---

## Notes

- [P] tasks = different files, no dependencies, safe to parallelize
- [Story] label maps task to specific user story for traceability
- Each user story is independently testable and committable
- Migration (T023-T026) is the highest risk task - test thoroughly in local SQLite before production deployment
- Use `dotnet ef migrations list` to verify migration history after applying RemoveLinkToken migration
- Verify tests after EACH user story completion, not just at the end
- Commit after each user story completion for clean git history
- Stop at any checkpoint to validate story independently before proceeding

---

## Success Criteria Validation

After all phases complete, verify all success criteria from spec.md:

- [ ] SC-001: Zero compilation errors or warnings (`dotnet build`)
- [ ] SC-002: All C# frontend tests pass (≥251 passing, 9 skipped)
- [ ] SC-003: All C# backend tests pass (zero failures)
- [ ] SC-004: All JavaScript tests pass (≥222 passing)
- [ ] SC-005: Migration successfully removes LinkToken column
- [ ] SC-006: `dotnet ef migrations list` shows RemoveLinkToken migration
- [ ] SC-007: Session API response has adminToken/singerToken, NOT linkToken
- [ ] SC-008: Code search for "LinkToken"/"linkToken" returns zero results (except migrations)
- [ ] SC-009: No "DEPRECATED" comments remain
- [ ] SC-010: QR code URLs use `?session={guid}&token={token}` format only
