# Karamel-Web Agent Notes

## Stack

- .NET 10 / C# 14: Blazor WebAssembly frontend and ASP.NET Core backend.
- Fluxor manages frontend state; the backend exposes REST APIs and a SignalR hub.
- JavaScript ES modules support browser media access and playback; JS tests use Vitest.
- Backend persistence is ephemeral SQLite. Startup recreates the schema with
  `EnsureDeleted()` and `EnsureCreated()`; there are no EF migrations or SQL Server
  provider.

## Important constraints

- Sessions are created by the backend and isolated by session ID. Secondary devices
  get data from the backend API or SignalR; local file paths remain on the host tab.
- When changing song fields, preserve the full JavaScript/C# serialization contract.
  See [serialization.instructions.md](../instructions/serialization.instructions.md).
- For playlist status rules, see
  [playlist-status-system.instructions.md](../instructions/playlist-status-system.instructions.md).

## Validation

Run commands from the repository root:

```powershell
dotnet build
dotnet test Karamel.Web.Tests
dotnet test Karamel.Backend.Tests -v minimal
Push-Location Karamel.Web\wwwroot
npm run test:run
Pop-Location
```

For general project architecture and workflows, see
[Copilot instructions](../copilot-instructions.md). Load focused instruction files
when modifying the areas they cover.
