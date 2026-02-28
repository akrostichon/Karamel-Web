# Research Notes for Admin Session Control

## Decision: Session settings group box with gear icon

The admin panel will include a `SessionControls` component which renders inside a styled `div` resembling a grouped box. To make it visually distinct, a gear icon (⚙️) will appear in the header of the box next to the title "Session Settings". The gear icon aligns with existing use of the gear symbol throughout the application (player/next song icon, session setup instructions). No new icon library is required; the plain Unicode gear is already in use in NextSongView and PlayerView.

## Decision: Play/Pause icon design

Instead of a traditional pause symbol (two vertical bars), the pause button will show a play triangle followed by a vertical bar (`▶️|`) to convey "play with stop" and match the request. The resume button will show a simple play triangle (`▶️`). These can be rendered as text inside the button or using Bootstrap icons if available; existing pages use emoji for other inline icons, so text-based icons are acceptable and maintain visual consistency.

## Session configuration storage and propagation

The four runtime flags will be added to `SessionConfig` DTO and persisted in the Sessions table via EF Core migration. Backend endpoints already return session DTO for library operations; extend these to include the new fields. SignalR hub already broadcasts session updates (e.g., settings changes) so reuse the `ReceiveSessionSettings` event or create new `ReceiveConfigUpdated` event. Pause/resume events require new hub methods and corresponding client state.

## Paused flag implementation

Frontend should maintain `bool IsPaused` in `SessionState` or a new slice `SessionControlState`. The pause/resume SignalR events will dispatch Fluxor actions to set this flag. Components that drive progression (e.g., playlist advancement logic) will check this flag and skip automatic advancement when paused.

## UI placement and segmented control

Admin tabs use `PlaylistView.razor`. A segmented control already exists for playlist vs settings. The `SessionControls` component lives inside the settings tab. Initially configuration inputs are disabled; they become enabled after second iteration. Use CSS classes to hide segmented control for non-admin tabs. Gear icon in the group header helps visually link the box to the admin gear usage elsewhere.

## Testing strategies

C# unit tests for SignalR hub methods (pause/resume, config updates) already exist; add new tests in `PlaylistHubTests` or new test files. Component tests (`AdminControlsTests`, etc.) will verify UI shows correct icons and group box, toggling between pause/resume icons works correctly. JavaScript tests may need updates if new interop modules are added (unlikely).

## Alternatives considered

- Using SVG icons or Bootstrap icons for play/pause: rejected for simplicity, unicode emojis suffice and are already used.
- Storing pause state on backend: rejected per spec requirement transient only.

## Rationale

The decisions align with existing code patterns (unicode gear, emoji icons) and keep the UI lightweight. Using existing SignalR events and session DTO extensions minimizes backend changes. Placing settings in a group box with gear emphasizes admin-only context while preserving the primary playlist UI.