# Karamel-Web Copilot Instructions

## Project Overview
Karamel-Web is a modern karaoke system consisting of a Blazor WebAssembly frontend and an ASP.NET Core backend. The frontend (Karamel.Web) runs in the browser and provides the UI and playback features; the backend (Karamel.Backend) exposes REST endpoints and a SignalR hub used for session coordination and multi-user scenarios. The app uses the **File System Access API** (Chrome/Edge only) for local media file access and the **Broadcast Channel API** for cross-tab state synchronization. Key features include singer management, playlist control, CDG+MP3 playback, and session sharing via QR codes.
Karamel-Web is a modern karaoke system consisting of a Blazor WebAssembly frontend and an ASP.NET Core backend. The frontend (Karamel.Web) runs in the browser and provides the UI and playback features; the backend (Karamel.Backend) exposes REST endpoints and a SignalR hub used for session coordination and multi-user scenarios. The app uses the **File System Access API** (Chrome/Edge only) for local media file access and a **SignalR-based session synchronization** mechanism for cross-tab state synchronization. Key features include singer management, playlist control, CDG+MP3 playback, and session sharing via QR codes.

**Repository size**: Small (≈30–100 source files)  
**Languages**: C# (Blazor frontend + ASP.NET Core backend), JavaScript (ES modules), CSS  
**Target runtime**: Browser (WebAssembly) for frontend, .NET 10.0 / ASP.NET Core for backend  
**State management**: Fluxor (Redux pattern)  

## Critical Build & Test Commands

### Build Commands
**IMPORTANT**: Always build from the solution root directory (`D:\Projects\Karamel-Web`).

```powershell
# Clean build (recommended after changes)
dotnet clean
dotnet build

# Expected warnings: 1 warning (CS8602 in SingerView.razor line 40) is known and acceptable
# Build time: ~6-7 seconds on first build, ~2-3 seconds incremental for frontend; backend build can add a few seconds on first run
```

### Test Commands
**C# Frontend Tests** (xUnit + bUnit):
```powershell
dotnet test Karamel.Web.Tests
# Runs 104 tests in Karamel.Web.Tests (101 pass, 3 skipped by design)
# Test time: ~8-10 seconds
# Known skipped tests: 2 in PlaylistPageTests (async JSInterop mocking limitations), 1 in PlayerViewTests (session validation changes)
```

**C# Backend Tests** (xUnit + Integration Tests):
```powershell
# MUST run MANUALLY - do not execute via run_in_terminal tool
# Test time: ~35 seconds (SignalR WebSocket tests can cause VS Code freezes if run programmatically)
# Command: dotnet test .\Karamel.Backend.Tests\ -v minimal
```
**CRITICAL FOR COPILOT**: 
- **NEVER run backend tests using run_in_terminal tool** - they can freeze or crash VS Code due to SignalR WebSocket resource contention
- Always ask the user to run `dotnet test .\Karamel.Backend.Tests\ -v minimal` manually
- Wait for user to provide test output before proceeding
- Do not use `dotnet test --no-build` - always run `dotnet test` to include the build step

**JavaScript Tests** (Vitest):
```powershell
cd Karamel.Web\wwwroot
npm run test:run    # Single run (NOT: npm test:run)
# Runs > 127 tests
# Test time: ~3 seconds
# Watch mode: npm test
```

**CRITICAL**: Always run both test suites before committing. C# tests must pass with maximum 3 allowed skips; JavaScript tests should have zero failures.

### Run Application
```powershell
dotnet run --project Karamel.Web
# Launches on http://localhost:5245
# Browser must support File System Access API (Chrome/Edge only)
# Application start time: ~2-3 seconds
```

## Development Workflow

### Branch Strategy
**MANDATORY**: Never commit directly to `main`. Always create a feature branch:
```powershell
git checkout -b feature/your-feature-name
# Make changes and commit
git push origin feature/your-feature-name
# DO NOT merge to main - push the branch and stop
```

### Before Making Changes
1. Verify current branch: `git branch` (should NOT be `main`)
2. Run `dotnet build` to ensure clean starting state
3. Run `dotnet test Karamel.Web.Tests` to verify baseline test results

### After Making Changes
1. Run `dotnet build` and resolve any errors
2. Run `dotnet test Karamel.Web.Tests` and ensure at least 101 tests pass
3. For JavaScript changes: `cd Karamel.Web\wwwroot; npm run test:run`
4. **For backend changes**: Request user to manually run `dotnet test .\Karamel.Backend.Tests\ -v minimal`
5. Test the running application manually if UI changes were made
6. If you are handling a step in an md file (e.g., DEVELOPMENT_PLAN.md), update the status accordingly

## Project Architecture

### Directory Structure
```
Karamel-Web/                          # Solution root
├── Karamel-Web.sln                   # Solution file
├── Karamel.Web/                      # Main Blazor WebAssembly project
│   ├── Program.cs                    # App entry point, Fluxor + services registration
│   ├── App.razor                     # Root component
│   ├── Components/                   # Reusable UI components
│   │   └── LibrarySearch.razor       # Song search component with debouncing
│   ├── Layout/                       # App layout components
│   │   ├── MainLayout.razor          # Primary layout
│   │   └── NavMenu.razor             # Navigation menu
│   ├── Models/                       # Domain models
│   │   ├── Session.cs                # Session configuration (multi-session support)
│   │   └── Song.cs                   # Song metadata
│   ├── Pages/                        # Routable page components
│   │   ├── Home.razor                # Session initialization & library selection
│   │   ├── PlayerView.razor          # CDG+MP3 playback view
│   │   ├── NextSongView.razor        # Next song display with QR code
│   │   ├── Playlist.razor            # Admin playlist management
│   │   └── SingerView.razor          # Singer song selection
│   ├── Services/                     # Application services
│   │   ├── ISessionService.cs        # Interface for session management
│   │   └── SessionService.cs         # SignalR session bridge & sessionStorage wrapper
│   ├── Store/                        # Fluxor state management
│   │   ├── Library/                  # Library state (song collection)
│   │   ├── Playlist/                 # Playlist state (queue, current song)
│   │   └── Session/                  # Session state (configuration, multi-session)
│   └── wwwroot/                      # Static assets
│       ├── index.html                # SPA entry point
│       ├── package.json              # JavaScript dependencies (Vitest)
│       ├── vitest.config.js          # Vitest configuration
│       ├── js/                       # JavaScript modules
│       │   ├── fileAccess.js         # File System Access API wrapper
│       │   ├── metadata.js           # ID3 tag extraction (jsmediatags)
│       │   ├── signalRBridge.js      # SignalR bridge (shim currently re-exports sessionBridge)
│       │   ├── homeInterop.js        # Home page JS interop
│       │   ├── player.js             # CDGraphics.js integration
│       │   ├── qrcode.js             # QR code generation
│       │   └── *.test.js             # Vitest test files
│       └── css/                      # Styling files
├── Karamel.Web.Tests/                # C# frontend test project (xUnit + bUnit)
│   ├── *Tests.cs                     # Component and integration tests
│   └── TestHelpers/                  # Mock utilities
├── Karamel.Backend/                  # ASP.NET Core backend (SignalR + REST API)
│   ├── Program.cs                    # Backend entry point
│   ├── Controllers/                  # REST API controllers
│   ├── Hubs/                         # SignalR hubs (PlaylistHub)
│   ├── Models/                       # Backend domain models
│   ├── Repositories/                 # Data access layer (EF Core)
│   └── Services/                     # Backend services (token management)
└── Karamel.Backend.Tests/            # C# backend test project (xUnit + Integration)
    ├── *Tests.cs                     # SignalR hub tests, REST API tests
    └── TestServerFactory.cs          # WebApplicationFactory for integration tests
```

### State Management (Fluxor)
- **LibraryState**: Song library (loaded from File System Access API)
- **PlaylistState**: Queue (songs to play), current song, singer statistics
- **SessionState**: Session configuration, multi-session isolation

**Actions** follow `<Feature><Action>` naming (e.g., `LoadLibrarySuccess`, `AddSongToPlaylist`).  
**Reducers** are pure functions in `*Reducers.cs` files.  
**Effects** for async operations are in `*Effects.cs` (e.g., `PlaylistEffects.cs`).

### Key Architectural Patterns

#### Multi-Session Architecture
**CRITICAL**: The app supports **multiple independent karaoke sessions** in different browser tabs/windows simultaneously. Each session:
- Has a unique `SessionId` GUID (passed via `?session={guid}` query parameter)
- Uses SignalR session groups (server-backed) for real-time synchronization
- Uses session-specific sessionStorage keys (`karamel-session-{guid}`) for persisted snapshot exchange
- Has its own directory handle (kept in JavaScript module scope in the main tab)

**Main tab** (Home page with directory access) remains the authoritative source of local file handles; other tabs use SignalR to receive live updates and sessionStorage for initial snapshots when necessary.

#### File System Access API
- **Main tab only** retains directory handle in JavaScript module scope (`fileAccess.js`)
- Other tabs receive song metadata via sessionStorage (no file access)
- `loadSongFiles(mp3FileName, cdgFileName)` loads files for playback from main tab's handle

#### Session Parameter Validation
**ALL pages** except Home.razor must:
1. Accept `[Parameter] [SupplyParameterFromQuery(Name = "session")] public string? SessionParam`
2. Validate session ID matches `SessionState.Value.CurrentSession.SessionId`
3. Show error message if validation fails

### CSS Architecture
- Component-scoped CSS: `*.razor.css` files (e.g., `PlayerView.razor.css`)
- Follow **STYLING_GUIDE.md** for colors, typography, and responsive design
- Use 8px spacing grid, caramel color palette (#EAAE63, #B23A48, etc.)

## Common Patterns & Conventions

### Razor Component Structure
```csharp
@page "/path"
@using Fluxor
@using Fluxor.Blazor.Web.Components
@inherits FluxorComponent
@implements IAsyncDisposable

@inject IState<SomeState> SomeState
@inject IDispatcher Dispatcher

<PageTitle>Page Title - Karamel Karaoke</PageTitle>

<!-- Markup here -->

@code {
    [Parameter]
    [SupplyParameterFromQuery(Name = "session")]
    public string? SessionParam { get; set; }

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        // Validate session, subscribe to state changes
    }

    protected override async ValueTask DisposeAsyncCore(bool disposing)
    {
        if (disposing)
        {
            // Unsubscribe, dispose resources
        }
        await base.DisposeAsyncCore(disposing);
    }
}
```

### Fluxor Action/Reducer Pattern
```csharp
// Actions.cs
public record LoadLibraryAction(IEnumerable<Song> Songs);

// Reducers.cs
[ReducerMethod]
public static LibraryState ReduceLoadLibrary(LibraryState state, LoadLibraryAction action) =>
    state with { Songs = action.Songs.ToList(), IsLoading = false };
```

### JavaScript Interop
```csharp
// C# side
await JSRuntime.InvokeAsync<IJSObjectReference>("import", "./js/module.js");
await module.InvokeVoidAsync("functionName", arg1, arg2);

// JavaScript side (ES module)
export function functionName(arg1, arg2) { /* ... */ }
```

## Testing Patterns

### C# Component Tests (bUnit)
```csharp
using Bunit;
using Fluxor;
using Xunit;

public class ComponentTests : TestContext
{
    [Fact]
    public void Component_RendersCorrectly()
    {
        // Arrange
        var store = Services.AddFluxor(...);
        Services.AddSingleton<ISessionService>(mockService);
        
        // Act
        var cut = RenderComponent<MyComponent>(parameters => parameters
            .Add(p => p.SessionParam, sessionId.ToString()));
        
        // Assert
        cut.Find(".expected-class").Should().NotBeNull();
    }
}
```

### JavaScript Tests (Vitest)
```javascript
import { describe, it, expect, beforeEach, vi } from 'vitest';

describe('moduleName', () => {
  beforeEach(() => {
    // Reset mocks
    global.BroadcastChannel = vi.fn();
  });

  it('should do something', () => {
    const result = functionUnderTest(input);
    expect(result).toBe(expected);
  });
});
```

## Known Issues & Workarounds

### Build Warnings
- **CS8602 warning in SingerView.razor:40**: Dereference warning - acceptable, not blocking
- One build warning is expected and normal

### Test Failures
- **C# tests**: 3 skipped tests (PlaylistPageTests x2, PlayerViewTests x1) due to bUnit async JSInterop limitations - this is intentional
- **JavaScript tests**: 7 known failures in homeInterop/sessionBridge tests - minor assertion mismatches, not critical

### Browser Compatibility
- **File System Access API** requires Chrome/Edge (not supported in Firefox/Safari)
- Localhost detection: `host == "localhost" || host == "127.0.0.1" || host == "::1"`

### State Synchronization
- **Main tab must remain open** - closing it ends the session for all tabs (loses directory handle)
- Use `StateHasChanged()` after Fluxor state changes in component event handlers
- Subscribe to `State.StateChanged` events in `OnInitializedAsync`, unsubscribe in `DisposeAsyncCore`

## Configuration Files

### Karamel.Web.csproj
- Target: `net10.0`, Blazor WebAssembly SDK
- NuGet: Fluxor.Blazor.Web 6.9.0, ASP.NET Core 10.0.1

### Karamel.Web.Tests.csproj
- Target: `net10.0`
- NuGet: xUnit 2.9.3, bUnit 1.32.7, Moq 4.20.72

### wwwroot/package.json
- Vitest 1.0.0, Happy-DOM 12.10.3
- Scripts: `npm test` (watch), `npm run test:run` (once), `npm run test:ui` (UI)

### wwwroot/vitest.config.js
- Environment: `happy-dom`
- Coverage: v8 provider

## Important Files Reference

**Entry points**:
- `Karamel.Web/Program.cs`: Service registration (Fluxor, SessionService)
- `Karamel.Web/App.razor`: Root component
- `Karamel.Web/wwwroot/index.html`: HTML entry point

**State management**:
- `Store/Library/LibraryState.cs`, `LibraryActions.cs`, `LibraryReducers.cs`
- `Store/Playlist/PlaylistState.cs`, `PlaylistActions.cs`, `PlaylistReducers.cs`, `PlaylistEffects.cs`
- `Store/Session/SessionState.cs`, `SessionActions.cs`, `SessionReducers.cs`

**JavaScript modules**:
-- `wwwroot/js/fileAccess.js`: Directory scanning, file loading (File System Access API)
-- `wwwroot/js/signalRBridge.js`: SignalR bridge
- `wwwroot/js/metadata.js`: ID3 tag extraction (jsmediatags from CDN)
- `wwwroot/js/player.js`: CDG+MP3 playback (cdgraphics.js integration)

**Core pages**:
- `Pages/Home.razor`: Session creation, library selection
- `Pages/PlayerView.razor`: Karaoke player (CDG + MP3 sync)
- `Pages/NextSongView.razor`: Stage display (next song + QR code)
- `Pages/SingerView.razor`: Singer interface (search + add to queue)
- `Pages/Playlist.razor`: Admin queue management (drag-drop)

## Documentation Files
- **DEVELOPMENT_PLAN.md**: Feature roadmap, implementation rules, phase tracking
- **TESTING_STRATEGY.md**: Test patterns, conventions, coverage goals
- **STYLING_GUIDE.md**: UI design, color palette, typography, responsive layout
- **README.md**: Project overview, feature list

## Final Reminders
1. **NEVER commit to `main`** - always use feature branches
2. **Run tests before committing** - `dotnet test` must show 101+ passing
3. **Session parameter is mandatory** on all pages except Home.razor
4. **Multi-session support**: Each session is isolated by GUID in URLs, channels, and storage
5. **Trust these instructions** - only search the codebase if specific implementation details are unclear or need to be verified
