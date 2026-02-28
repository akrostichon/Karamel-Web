# Tasks for Admin Session Control

## Phase 1: Setup

- [X] T001 [P] Add new properties to `Karamel.Backend.Models.SessionConfig` and create an EF Core migration (`Migrations/`) for `RequireSingerName`, `AllowSingersToReorder`, `PauseBetweenSongsSeconds`, and `Theme`.
  *already present from prior work; migration existing*
- [X] T002 [P] Update `SessionsController` (create/get endpoints) to accept and return flattened configuration flags, including new properties. Add necessary request DTOs in the same file.
  *already implemented*
- [X] T003 [P] Extend backend `Contracts` (create `SessionConfigDto` if missing) with camelCase properties matching the new config fields and implement conversion helpers in an appropriate helper class.
- [X] T004 [P] Update frontend `Karamel.Web.Services.SessionApiClient` and `PlaylistStateSynchronizer` to parse the new JSON properties (`requireSingerName`, `allowSingersToReorder`, `pauseBetweenSongsSeconds`, `theme`).
  *already implemented*
- [X] T005 [P] Extend `Session` model in `Karamel.Web.Models` with the corresponding properties and adjust any existing constructors or factory methods.
  *already present*
- [X] T006 [P] Add Fluxor actions (`PauseSessionAction`, `ResumeSessionAction`, `SessionConfigUpdatedAction`) and update `SessionState` to include `bool IsPaused` and a config DTO/fields. Add initial reducers for these actions.

## Phase 2: Foundational Tasks

- [X] T007 Implement new SignalR hub methods in `PlaylistHub`: `PauseSessionAsync`, `ResumeSessionAsync`, and `UpdateSessionConfigAsync`. Ensure each validates admin token and broadcasts corresponding client events (`ReceiveSessionPaused`, `ReceiveSessionResumed`, `ReceiveConfigUpdated`).
- [X] T008 Add server-side logging for the new hub methods and for config updates using structured logging.
- [X] T009 Implement backend repository method to update session configuration when `UpdateSessionConfigAsync` is called and persist to database.
- [X] T010 Add integration tests in `Karamel.Backend.Tests` for new hub methods (pause/resume/config) and repository behavior.

## Phase 3: User Story 1 – Session lifecycle commands

**Goal:** Admin can pause, resume and advance the playlist from any tab; all clients respond correctly.

**Independent test criteria:** Open multiple admin tabs and one singer tab; sending pause/resume/next from any admin tab updates all clients appropriately; non-admin tab ignores pause/resume.

- [X] T011 Add UI buttons for pause (`▶️|`), resume (`▶️`), and next to `SessionControls` component (initially hidden). Buttons dispatch Fluxor actions or call hub methods.
- [X] T012 Implement effect handlers for pause/resume actions that invoke corresponding hub methods and update state when receiving broadcast events.
- [X] T013 Update playlist advancement logic (both automatic and manual) to check `SessionState.IsPaused` and suppress progression while paused.
- [X] T014 Write component tests in `Karamel.Web.Tests` validating that clicking the pause/resume buttons toggles icons, dispatches actions, and that non-admin tabs do not render the controls.
- [X] T015 Add cross-tab/integration test verifying pause/resume events propagate via SignalR and affect playlist advancement state.
- [X] T035 Fix `NextSongView` progress-bar and advancement when session is paused:
  - When the session is paused (either on load or after `ReceiveSessionPaused` is received), do **not** start the countdown timer and hide the progress bar.
  - When `ResumeSessionAction` is dispatched (or `ReceiveSessionResumed` arrives), start the countdown timer and show the progress bar if an `UpNext` song is present.
  - When a `ReceiveSessionPaused` event arrives **while a countdown is already actively running**, **cancel** the countdown immediately (stop timers, reset `isTimerActive`, clear `_currentNextSongId`). On resume, the full countdown restarts from scratch. (Allowing the timer to fire while paused would suppress `AdvanceToNextSongAsync`, leaving `CurrentSong` unset and causing a silent playback timeout.)
  - Write/update component tests in `Karamel.Web.Tests` for each of these three sub-behaviors.
- [X] T036 Hide next-song content in `NextSongView` while session is paused:
  - When `SessionState.IsPaused` is `true`, render the empty-queue ("Sing a Song!" + QR code / link) layout regardless of how many songs are in the queue — change the render condition from `nextSong != null` to `nextSong != null && !SessionState.Value.IsPaused`.
  - When the session is resumed, the next-song card reappears immediately (no additional action needed; `IsPaused` becomes `false` and re-render kicks in).
  - Write component tests verifying: (a) when paused with a queued song, the song card is absent and the empty-queue layout is shown; (b) when not paused with a queued song, the song card is shown; (c) on session resume, the song card reappears.

## Phase 4: User Story 2 – Runtime configuration

**Goal:** Admin toggles settings; changes propagate and new tabs inherit them.

**Independent test criteria:** Modify each config option and verify existing and new tabs see the change and behavior enforces it (e.g. singer name required blocks adds, reorder toggles, pause delay applied, theme changes).

- [X] T016 Extend `SessionControls` UI with four inputs: singer-name-required checkbox, allow-reorder checkbox, numeric input for pause-between-songs, theme selector. Initially disabled until iteration 3.2.
- [X] T017 Implement validation for the pause-between-songs field (integer ≥5 ≤90, default 0 if empty); enforce on save and in backend DTO.
- [X] T018 Add Fluxor effect to send config updates to the hub via `UpdateSessionConfigAsync` when admin saves; handle `ReceiveConfigUpdated` broadcast to update state.
- [X] T019 Modify client logic to enforce new flags: block song addition when `RequireSingerName` true; hide/disable drag handles for singers when `AllowSingersToReorder` false; apply theme changes immediately by updating `localStorage` or service.
- [X] T020 Write component/unit tests verifying that toggling each setting updates state, sends hub call, and that other clients receive and react to the update.
- [X] T021 Add backend API or hub integration tests ensuring config persistence and broadcast behavior.

## Phase 5: User Story 3 – Admin UI placement & phased activation

**Goal:** Segmented control on PlaylistView toggles between playlist and session control panel; iteration 3.1 shows only lifecycle buttons, iteration 3.2 enables full config and back button.

**Independent test criteria:** Admin tab shows segmented control; switching to session control displays appropriate UI for iteration; non-admin tabs do not show control.

- [ ] T022 Modify `Playlist.razor` to render segmented control when `_isAdminTab` true and handle selection state.
- [ ] T023 Create new `SessionControls.razor` component to host the group box with gear icon header and the buttons/inputs from previous tasks.
- [ ] T024 In iteration 3.1, disable configuration inputs and hide the back-to-playlist button; ensure only pause/resume/next are active.
- [ ] T025 In iteration 3.2, enable inputs and add a "Back to playlist" button that switches the segment.
- [ ] T026 Add component tests confirming the segmented control appears only for admin, segments toggle content, and iteration-specific behaviour works.

## Phase 6: User Story 4 – SingerView read‑only playlist mode

**Goal:** Any user on SingerView can toggle to see a read-only copy of the up-next list.

**Independent test criteria:** Toggling between Library and Playlist shows the correct view; playlist has no remove/reorder controls.

- [ ] T027 Add a toggle button on `SingerView.razor` that switches between the existing library search and an `UpNextList` component.
- [ ] T028 Ensure `UpNextList` renders playlist items without drag handles or remove buttons when shown in singer mode.
- [ ] T029 Write tests verifying the toggle works and that the list is read-only.

## Final Phase: Polish & cross-cutting concerns

- [ ] T030 Add styling for the session settings group box with a gear icon header; ensure icons match design (`▶️|` pause, `▶️` resume).
- [ ] T031 Add documentation updates (README or comments) describing the admin link and controls.
- [ ] T032 Run full test suites (`dotnet test` for both projects and `npm run test:run`) and fix any failures.
- [ ] T033 Manually verify multi-tab scenarios and update any telemetry/logging as needed.
- [ ] T034 Review and commit changes on a feature branch following git workflow rules.

### Dependencies

1. Phase 1 tasks establish the new data model and API surface; all subsequent tasks depend on them.
2. Phase 2 foundation must complete before US1 and US2 tasks can function (hub methods, state updates).
3. US1 and US2 are largely independent but share the foundation; they can proceed in parallel after Phase 2.
4. US3 builds on US1/US2 by providing the UI wrapper; iterate 3.1 can occur concurrently with US1/US2, iteration 3.2 requires their core features.
5. US4 is independent of the admin-specific work and may run in parallel with earlier user stories.

### Parallel execution examples

- While T007–T010 are in progress, another developer can start T011–T015 (lifecycle UI) or T027–T029 (singer view toggle).
- Styling and documentation (T030, T031) can run alongside any feature work.

### Independent test criteria recap

- **US1**: Admin pause/resume/next works across tabs and non-admin ignorance.
- **US2**: Runtime toggles propagate and enforce behavior, new tabs inherit state.
- **US3**: Segmented control renders correctly and toggles session panel; iteration gating observed.
- **US4**: Singer view toggle shows read‑only playlist without controls.

### Suggested MVP

Start by completing US1 lifecycle controls (T011–T015) paired with foundational hub implementation (T007–T010). US2 and US3 can follow once pause/resume works reliably.


