---
name: karamel-code-review
description: "Karamel-Web-specific review checks for session isolation, file-path privacy, serialization, playlist state, and hosting constraints. Use when reviewing Karamel-Web changes, especially with a two-axis code-review workflow; apply only checks relevant to the diff."
---

# Karamel-Web Review Supplement

Use these checks as the project-specific standards lens for a Karamel-Web diff. A review workflow such as Matt Pocock's `code-review` skill handles scope, spec compliance, and report structure; this skill supplies Karamel-Web invariants. Keep Standards and Spec findings separate, and treat the repository instructions linked below as authoritative.

Review only changed lines for findings. Read surrounding code and the relevant project instructions for context. Apply only checks relevant to the change, and cite the source rule and changed hunk for each finding.

## Project invariants

- **Sessions and devices:** Session IDs come from the backend. Keep state isolated by session. Secondary tabs and devices get primary data from the backend API or SignalR; sessionStorage and BroadcastChannel are same-device optimizations, not cross-device sources of truth. Pages other than `Home.razor` validate the `session` query parameter against the active session.
- **File privacy:** File paths and local file handles stay on the main tab. Never send paths to the backend or put them in metadata, logs, or shared playlist state. Secondary clients are display-only and must not assume local file access.
- **Song serialization:** When a song field changes, trace it across the relevant JavaScript builders, DTO attributes, both conversion directions, backend JSON root-property fallback, SignalR `items` and `currentSong` maps, and both `PlaylistItemDto` construction paths. Check the matching serialization guidance and tests.
- **Playlist lifecycle:** A session has one playlist, identified by the session ID. Preserve the `Queued` → `UpNext` → `NowPlaying` → `Completed` lifecycle and backend auto-promotion; do not make a client-side copy of backend-owned transitions.
- **State boundaries:** Fluxor Effects orchestrate workflows and dispatch actions. Services remain focused helpers and do not dispatch Fluxor actions. Preserve the existing SignalR/API source of truth for cross-device state.
- **JavaScript interop:** Keep C# invocation argument count and order aligned with the exported JavaScript function signature.
- **Hosting and persistence:** For deployment or backend persistence changes, preserve the single-instance constraint and ephemeral SQLite behavior. Do not introduce durable storage or EF migrations without updating the hosting decision.

## Project references

Read the relevant source when a changed hunk touches that area:

- [Copilot instructions](../../copilot-instructions.md) — session architecture, privacy, service boundaries, and page validation
- [Serialization instructions](../../instructions/serialization.instructions.md) — DTO and JavaScript/C# contracts
- [Playlist status instructions](../../instructions/playlist-status-system.instructions.md) — playlist lifecycle and state ownership
- [Hosting instructions](../../instructions/hosting.instructions.md) — deployment and persistence constraints

Use the repository's review workflow for severity, finding format, and final recommendation; this supplement does not define a second checklist or report format.
