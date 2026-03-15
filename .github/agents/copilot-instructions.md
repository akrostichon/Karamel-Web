# Karamel-Web Development Guidelines

Auto-generated from all feature plans. Last updated: 2026-02-22

## Active Technologies
- C# 14 / .NET 10 (backend + Blazor WASM frontend), JavaScript ES2022 (browser modules) + Fluxor 6.9, ASP.NET Core 10, EF Core 10, xUnit, bUnit 1.32, Vitest 4.0, jsmediatags (001-song-duration-display)
- SQL Server (prod) / SQLite (dev) via EF Core  **no new database column or migration needed**; duration is stored as `durationSeconds` inside the existing `MetadataJson` JSON blob (001-song-duration-display)
- C# 14, .NET 10.0 + Blazor WebAssembly, ASP.NET Core, Fluxor 6.9.0, SignalR, Entity Framework Core (003-remove-deprecated-code)
- SQL Server (production), SQLite (development), File System Access API (browser-only, main tab) (003-remove-deprecated-code)
- C# 14 / .NET 10 (backend `Karamel.Backend`); Blazor WebAssembly / C# 14 (frontend `Karamel.Web`); JavaScript ES modules (`wwwroot/js/`) + ASP.NET Core 10, EF Core 10, Fluxor 6.9, SignalR, xUnit 2.9, bUnit 1.32, Vitest 4.0 (004-fuzzy-search)
- SQLite (local development via EF Core `Sqlite` provider); Azure SQL Server (`SqlServer` provider) in production; no schema migration needed (no new columns) (004-fuzzy-search)
- [e.g., Python 3.11, Swift 5.9, Rust 1.75 or NEEDS CLARIFICATION] + [e.g., FastAPI, UIKit, LLVM or NEEDS CLARIFICATION] (005-artist-exploration-browse)
- [if applicable, e.g., PostgreSQL, CoreData, files or N/A] (005-artist-exploration-browse)
- C# 14 / .NET 10 (both `Karamel.Backend` and `Karamel.Web`); JavaScript ES modules + ASP.NET Core 10, EF Core 10 (SQLite dev / SQL Server prod), Fluxor 6.9, SignalR, xUnit 2.9, bUnit 1.32 (005-artist-exploration-browse)
- SQLite (local dev); Azure SQL Server (production); no schema changes needed (queries against existing `Songs` table) (005-artist-exploration-browse)
- C# 14 / .NET 10; JavaScript (ES modules) + Blazor WebAssembly 10; Fluxor 6.9.0; Bootstrap 5; Vitest 4 (tests) (006-library-view-enhancements)
- N/A — pure frontend UI changes; no new persistence (006-library-view-enhancements)
- C# 14 / .NET 10, Blazor WebAssembly + Fluxor 6.9.0 (state), bUnit 1.32.7 (tests), Bootstrap 5 (layout), Vitest 4 (JS tests) (006-library-view-enhancements)
- N/A — both features are stateless UI changes (006-library-view-enhancements)
- C# 14 / .NET 10 (Blazor WebAssembly) + JavaScript ES modules + Fluxor 6.9.0, bUnit 1.32.7 (tests), Vitest 4.0.16 (JS tests) (007-library-ux-polish)
- No storage changes — scroll offset is ephemeral component-level state only (007-library-ux-polish)
- C# 14 / .NET 10 (backend), CSS3 (frontend styling) + Entity Framework Core (backend), Blazor CSS isolation (frontend) (008-library-search-fixes)
- SQLite (dev), SQL Server (prod) — fix is EF LINQ, tested on SQLite (008-library-search-fixes)
- C# 13 / .NET 10.0 (Blazor WebAssembly), JavaScript ES2022 (modules) + Blazor WebAssembly, Fluxor, Bootstrap Icons v1.x, IJSObjectReference (JS interop) (001-player-next-prev)
- N/A — no new persistence (001-player-next-prev)
- C# 14 / .NET 10 (Blazor WebAssembly) + Blazor WebAssembly SDK; existing `fileAccess.js` (reuse `pickLibraryDirectory`); new `exportBridge.js` JS module (feature/011-library-csv-export)
- Local component state only — no Fluxor `LibraryState`, no `sessionStorage`, no backend (feature/011-library-csv-export)

- C# 10/.NET 10 for backend and Blazor WebAssembly for frontend; JavaScript modules for client interop. + ASP.NET Core, SignalR, Fluxor on the frontend, xUnit/bUnit tests, Vitest for JS. (001-admin-session-control)

## Project Structure

```text
backend/
frontend/
tests/
```

## Commands

npm test; npm run lint

## Code Style

C# 10/.NET 10 for backend and Blazor WebAssembly for frontend; JavaScript modules for client interop.: Follow standard conventions

## Recent Changes
- feature/011-library-csv-export: Added C# 14 / .NET 10 (Blazor WebAssembly) + Blazor WebAssembly SDK; existing `fileAccess.js` (reuse `pickLibraryDirectory`); new `exportBridge.js` JS module
- 001-player-next-prev: Added C# 13 / .NET 10.0 (Blazor WebAssembly), JavaScript ES2022 (modules) + Blazor WebAssembly, Fluxor, Bootstrap Icons v1.x, IJSObjectReference (JS interop)
- 008-library-search-fixes: Added C# 14 / .NET 10 (backend), CSS3 (frontend styling) + Entity Framework Core (backend), Blazor CSS isolation (frontend)


<!-- MANUAL ADDITIONS START -->
<!-- MANUAL ADDITIONS END -->
