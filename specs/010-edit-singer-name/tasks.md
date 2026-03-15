# Tasks: Edit Singer Name in SingerView

**Input**: Design documents from `specs/010-edit-singer-name/`  
**Branch**: `feature/010-edit-singer-name`  
**Tech stack**: Blazor WebAssembly (C# 13 / .NET 10), xUnit + bUnit  
**Source files**: `Karamel.Web/Pages/SingerView.razor`, `Karamel.Web/Pages/SingerView.razor.css`, `Karamel.Web.Tests/SingerViewTests.cs`  
**No new services, no new Fluxor actions, no backend changes, no JS changes.**

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Parallelisable (touches a different file from other P tasks)
- **[Story]**: User story from spec.md (US1–US4)

---

## Phase 1: Setup

**Purpose**: Baseline build & test health confirmation before any changes.

- [X] T001 Confirm `dotnet build` passes with zero warnings (run from solution root)
- [X] T002 Confirm `dotnet test Karamel.Web.Tests` passes with ≥ 251 tests (9 skipped expected)

---

## Phase 2: User Story 1 — Singer Enters Edit Mode and Saves New Name (Priority: P1) 🎯 MVP

**Goal**: A singer can tap the name/pencil in the header, edit their name inline, and confirm to save it. All subsequent queue additions use the updated name.

**Independent Test**: Navigate to SingerView with `RequireSingerName = true` and a name already set. Tap the name or pencil icon → inline edit appears pre-filled. Type a new name, tap confirm → header shows updated name. Add a song → queue entry carries the new name.

### Tests for User Story 1

- [X] T003 [P] [US1] Add test `EditName_WhenPencilIconClicked_ShowsInlineEditMode` in `Karamel.Web.Tests/SingerViewTests.cs` — renders component with `RequireSingerName=true` and a pre-set `singerName` (bypass the name-entry form via reflection or direct render), clicks the pencil button, asserts an `input.singer-name-input` appears pre-filled with the current name
- [X] T004 [P] [US1] Add test `EditName_WhenNameClicked_ShowsInlineEditMode` — same setup, clicks the `h3` name text instead of the pencil, asserts edit mode opens
- [X] T005 [P] [US1] Add test `EditName_ConfirmWithValidName_SavesNameAndExitsEditMode` — enters edit mode, changes input value, clicks confirm button, asserts `singerName` field updated and pencil icon is visible again (edit mode closed)
- [X] T006 [P] [US1] Add test `EditName_UpdatedNameUsedForSubsequentQueueAdditions` — after a successful rename, invokes `HandleAddToQueue`, asserts `AddToPlaylistAction` is dispatched with the new singer name

### Implementation for User Story 1

- [X] T007 [US1] Add three private state fields to `SingerView.razor @code`: `isEditingName`, `editNameValue`, `editNameHasError` — see plan.md "State Variables" section in `Karamel.Web/Pages/SingerView.razor`
- [X] T008 [US1] Add `StartEditName()`, `ConfirmEditName()`, `HandleEditKeyUp()` methods to `SingerView.razor @code` per plan.md design — in `Karamel.Web/Pages/SingerView.razor`
- [X] T009 [US1] Replace the `<h3>Welcome, @singerName!</h3>` block in the `singer-header` with the conditional display/edit markup from plan.md (pencil icon button + inline input + confirm button) in `Karamel.Web/Pages/SingerView.razor`
- [X] T010 [P] [US1] Add `.singer-name-display`, `.singer-edit-btn`, `.singer-name-edit`, `.singer-name-input`, `.singer-name-confirm` CSS rules to `Karamel.Web/Pages/SingerView.razor.css` per plan.md
- [X] T011 [US1] Run failing tests (T003–T006) and fix until all pass; run `dotnet build` to confirm zero warnings

**Checkpoint**: US1 fully functional — a singer can rename themselves and the new name propagates to queue additions. ✅

---

## Phase 3: User Story 2 — Singer Cancels the Edit (Priority: P2)

**Goal**: Tapping outside the input (blur/focusout) cancels the edit and restores the original name without saving.

**Independent Test**: Enter edit mode, modify the text, click anywhere outside the input → header returns to original name.

### Tests for User Story 2

- [X] T012 [P] [US2] Add test `EditName_WhenFocusLost_CancelsEditAndRestoresOriginalName` in `SingerViewTests.cs` — enters edit mode, changes value in input, triggers `focusout` event on the input, asserts `singerName` unchanged and edit mode closed
- [X] T013 [P] [US2] Add test `EditName_CancelDoesNotSavePartialEdit` — same as above but verifies the partial modification is discarded and header shows original name text

### Implementation for User Story 2

- [X] T014 [US2] Add `CancelEditName()` method to `SingerView.razor @code` and wire `@onfocusout="CancelEditName"` on the name input; add `@onmousedown:preventDefault` on confirm button to prevent blur before click fires — in `Karamel.Web/Pages/SingerView.razor`
- [X] T015 [US2] Run failing tests (T012–T013) and fix until all pass

**Checkpoint**: US2 functional — accidental or deliberate focus-away correctly cancels without side effects. ✅

---

## Phase 4: User Story 3 — Empty Name Validation (Priority: P3)

**Goal**: Confirming an empty or whitespace-only name is rejected; the input shows a red-border error; edit mode stays active. Once valid name is entered and confirmed, name saves normally.

**Independent Test**: Enter edit mode, clear the input and click confirm → `is-invalid` CSS class appears on input and name is not changed. Type a valid name and confirm → saves successfully.

### Tests for User Story 3

- [X] T016 [P] [US3] Add test `EditName_ConfirmWithEmptyInput_DoesNotSaveAndShowsError` in `SingerViewTests.cs` — enters edit mode, clears the input, clicks confirm, asserts `singerName` unchanged, `editNameHasError` state true (via rendered `is-invalid` class on the input), and edit mode still active
- [X] T017 [P] [US3] Add test `EditName_ConfirmWithWhitespaceOnly_TreatedAsEmpty` — same but input contains spaces only
- [X] T018 [P] [US3] Add test `EditName_AfterErrorState_ValidNameSavesAndClearsError` — after empty-name rejection, types valid name, confirms → saves, error class removed

### Implementation for User Story 3

- [X] T019 [US3] Ensure `ConfirmEditName()` trims input and rejects whitespace-only values by setting `editNameHasError = true` without saving (already included in plan.md design); verify the `is-invalid` CSS class on the input is tied to `editNameHasError` in the markup — in `Karamel.Web/Pages/SingerView.razor`
- [X] T020 [US3] Run failing tests (T016–T018) and fix until all pass

**Checkpoint**: US3 functional — empty/whitespace names are blocked with visible feedback. ✅

---

## Phase 5: User Story 4 — Edit Controls Hidden When "Require Singer Name" Is Disabled (Priority: P4)

**Goal**: When `RequireSingerName` is `false`, no pencil icon appears and tapping the name area does not enter edit mode.

**Independent Test**: Open SingerView with `RequireSingerName=false` → no pencil button in DOM, clicking name area does nothing.

### Tests for User Story 4

- [X] T021 [P] [US4] Add test `EditName_WhenRequireSingerNameFalse_NoPencilIconRendered` in `SingerViewTests.cs` — renders with `RequireSingerName=false`, asserts `button.singer-edit-btn` throws `ElementNotFoundException`
- [X] T022 [P] [US4] Add test `EditName_WhenRequireSingerNameFalse_ClickingNameDoesNotTriggerEditMode` — renders with `RequireSingerName=false`, asserts no `input.singer-name-input` appears in DOM (edit mode unreachable)

### Implementation for User Story 4

- [X] T023 [US4] Verify that the `@if (SessionState.Value.CurrentSession?.RequireSingerName == true ...)` guard in the markup correctly excludes the edit UI (pencil and edit block) when false — the plan.md markup already includes this guard; review and confirm the non-`RequireSingerName` branch falls through to the plain `<h3>` — in `Karamel.Web/Pages/SingerView.razor`
- [X] T024 [US4] Run failing tests (T021–T022) and fix until all pass

**Checkpoint**: US4 functional — feature is fully gated behind `RequireSingerName`. ✅

---

## Phase 6: Polish & Cross-Cutting

- [X] T025 Run full test suite `dotnet test Karamel.Web.Tests` — all passing (≥ 251 + new tests), 9 skipped expected
- [X] T026 [P] Run `dotnet build` from solution root — confirm zero warnings
- [ ] T027 [P] Manual smoke test: open SingerView via QR link on a phone, verify pencil icon visible, rename works, confirm and cancel both behave correctly; verify with `RequireSingerName=false` that no pencil appears

---

## Dependencies

```
T001 → T002 (baseline confirmed)
T007 → T008 → T009 (state fields before methods before markup)
T010 ║ T009 (parallel — CSS vs markup)
T003-T006 → T011 (tests before fix loop)
T009 → T014 (cancel wired after markup exists)
T012-T013 → T015
T016-T018 → T020
T021-T022 → T024
T025 depends on T011, T015, T020, T024
```

## Parallel Opportunities Per Story

**US1**: T003, T004, T005, T006 (all test tasks), T010 (CSS) can be written in parallel before connecting to the implementation.  
**US2**: T012, T013 in parallel.  
**US3**: T016, T017, T018 in parallel.  
**US4**: T021, T022 in parallel.

## Implementation Strategy

**MVP (US1 + US2 only)**: Implement T001–T015. This gives a fully working rename flow with cancel support — the two highest-value scenarios. US3 (empty validation) and US4 (guard gate) can follow immediately after since they each require only 4–5 tasks.

**Recommended order**: US1 → US2 → US3 → US4 → Polish. Each phase independently testable before the next starts.
