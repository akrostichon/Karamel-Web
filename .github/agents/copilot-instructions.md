# Karamel-Web Development Guidelines

Auto-generated from all feature plans. Last updated: 2026-02-22

## Active Technologies
- C# 14 / .NET 10 (backend + Blazor WASM frontend), JavaScript ES2022 (browser modules) + Fluxor 6.9, ASP.NET Core 10, EF Core 10, xUnit, bUnit 1.32, Vitest 4.0, jsmediatags (001-song-duration-display)
- SQL Server (prod) / SQLite (dev) via EF Core  **no new database column or migration needed**; duration is stored as `durationSeconds` inside the existing `MetadataJson` JSON blob (001-song-duration-display)
- C# 14, .NET 10.0 + Blazor WebAssembly, ASP.NET Core, Fluxor 6.9.0, SignalR, Entity Framework Core (003-remove-deprecated-code)
- SQL Server (production), SQLite (development), File System Access API (browser-only, main tab) (003-remove-deprecated-code)

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
- 003-remove-deprecated-code: Added C# 14, .NET 10.0 + Blazor WebAssembly, ASP.NET Core, Fluxor 6.9.0, SignalR, Entity Framework Core
- 001-song-duration-display: Added C# 14 / .NET 10 (backend + Blazor WASM frontend), JavaScript ES2022 (browser modules) + Fluxor 6.9, ASP.NET Core 10, EF Core 10, xUnit, bUnit 1.32, Vitest 4.0, jsmediatags

- 001-admin-session-control: Added C# 10/.NET 10 for backend and Blazor WebAssembly for frontend; JavaScript modules for client interop. + ASP.NET Core, SignalR, Fluxor on the frontend, xUnit/bUnit tests, Vitest for JS.

<!-- MANUAL ADDITIONS START -->
<!-- MANUAL ADDITIONS END -->
