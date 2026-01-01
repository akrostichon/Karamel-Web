# Testing Strategy for Karamel-Web

## Overview

This document outlines the testing approach for Karamel-Web, covering JavaScript unit tests, C# unit tests, and Blazor component tests.

---

## Testing Stack

### JavaScript Testing (Current)
- **Framework**: Vitest v1.0.0
- **DOM Mock**: Happy-DOM v12.10.3
- **Location**: `wwwroot/js/*.test.js`
- **Command**: `npm test` (watch mode) or `npm run test:run` (single run)
- **UI**: `npm run test:ui` for visual test runner

### C# Testing (Current)
- **Framework**: xUnit v3.1.4
- **Test Project**: `Karamel.Web.Tests`
- **Location**: `Karamel.Web.Tests/*.cs`
- **Command**: `dotnet test`
- **Blazor Testing**: bUnit (future - for component tests)
- **Mocking**: Moq (future)

---

## Established Patterns

### JavaScript Unit Tests

Following patterns from `fileAccess.test.js` and `sessionBridge.test.js`:

1. **Mock browser APIs** (File System Access API, BroadcastChannel, sessionStorage)
2. **Test public module functions** with various inputs
3. **Verify state changes** and side effects
4. **Use descriptive test names** that explain behavior
5. **Group related tests** with `describe` blocks
6. **Clean up after tests** with `beforeEach`/`afterEach`

**Example Structure:**
```javascript
import { describe, it, expect, beforeEach, vi } from 'vitest';

describe('moduleName', () => {
  beforeEach(() => {
    // Reset mocks and state
  });

  it('should do something when condition is met', () => {
    // Arrange
    const input = 'test';
    
    // Act
    const result = functionUnderTest(input);
    
    // Assert
    expect(result).toBe(expectedValue);
  });
});
```

---

## Step 2.5: Home Page Testing Plan

### 1. JavaScript Module Tests

**File**: `wwwroot/js/homeInterop.js` (NEW - extract JS logic)  
**Tests**: `wwwroot/js/homeInterop.test.js` (NEW)

#### Purpose
Extract all JavaScript interop logic from `Home.razor` into a testable module.

#### Test Cases

```javascript
describe('Session Management', () => {
  it('generates unique session GUIDs')
  it('generates session URLs with correct format (/session?id={guid})')
  it('validates session ID format')
});

describe('Library Selection', () => {
  it('calls pickLibraryDirectory from fileAccess module')
  it('handles successful directory selection')
  it('handles user cancellation (no directory selected)')
  it('handles permission denied errors')
  it('handles browser compatibility errors')
});

describe('Session Configuration', () => {
  it('validates pause seconds (must be >= 0)')
  it('validates filename pattern (must contain %artist or %title)')
  it('stores configuration to session state')
  it('applies default values when not specified')
});

describe('Session Initialization', () => {
  it('calls initializeSession with generated GUID')
  it('saves library to sessionStorage')
  it('broadcasts session started event')
  it('prevents starting session without library')
});

describe('Tab/Window Management', () => {
  it('generates correct playlist URL with session ID')
  it('generates correct singer URL with session ID')
  it('opens new windows/tabs with correct URLs')
  it('returns navigation URL for current tab (NextSongView)')
});
```

#### Mock Strategy

```javascript
// Mock File System Access API
const mockDirectoryHandle = {
  kind: 'directory',
  name: 'TestLibrary'
};

// Mock window.open
global.window.open = vi.fn();

// Mock signalRBridge shim (re-exports sessionBridge during migration)
vi.mock('./signalRBridge.js', () => ({
  initializeSession: vi.fn(),
  saveLibraryToSessionStorage: vi.fn(),
  broadcastStateUpdate: vi.fn()
}));

// Mock fileAccess module
vi.mock('./fileAccess.js', () => ({
  pickLibraryDirectory: vi.fn(() => Promise.resolve(mockDirectoryHandle)),
  scanLibraryDirectory: vi.fn(() => Promise.resolve([/* song list */]))
}));
```

---

### 2. C# Fluxor Reducer Tests ✅

**Location**: `Karamel.Web.Tests/FluxorReducerSignatureTests.cs` (IMPLEMENTED)

#### Purpose
Validate that all Fluxor reducers follow the correct signature pattern. These tests catch configuration errors at test time instead of runtime.

#### Test Cases (All Passing)

```csharp
public class FluxorReducerSignatureTests
{
    [Fact]
    public void AllReducerMethods_ShouldHaveTwoParameters()
    {
        // Verifies all [ReducerMethod] methods have exactly 2 parameters
        // Catches errors like: ReduceAction(State state) - missing action parameter
    }

    [Fact]
    public void AllReducerMethods_ShouldHaveStateAsFirstParameter()
    {
        // Verifies first parameter is a State type (ends with "State")
    }

    [Fact]
    public void AllReducerMethods_ShouldHaveActionAsSecondParameter()
    {
        // Verifies second parameter is an Action type (ends with "Action")
    }

    [Fact]
    public void AllReducerMethods_ShouldBeStatic()
    {
        // Verifies all reducer methods are static
    }

    [Fact]
    public void AllReducerMethods_ShouldReturnStateType()
    {
        // Verifies return type is a State type (ends with "State")
    }

    [Fact]
    public void AllReducerMethods_FirstAndReturnTypeShouldMatch()
    {
        // Verifies input state type matches return state type
        // E.g., (LibraryState, Action) => LibraryState
    }

    [Fact]
    public void AllReducerClasses_ShouldBeStatic()
    {
        // Verifies all *Reducers classes are static
    }
}
```

**Running these tests:**
```bash
dotnet test
# or
dotnet test --filter FluxorReducerSignatureTests
```

#### What These Tests Catch

These tests would have caught the original error immediately:
```
❌ LibraryReducers.ReduceLoadLibraryAction has 1 parameters (expected 2)
```

Instead of the cryptic runtime error:
```
AggregateException_ctor_DefaultMessage (Method must have 2 parameters (state, action)...)
```

---

### 3. C# State Logic Tests (Future)

**Location**: `Karamel.Web.Tests/SessionStateTests.cs` (TODO)

#### Purpose
Validate session state management, reducers, and actions behavior.

#### Test Cases (Future)

```csharp
public class SessionStateTests
{
    [Fact]
    public void InitializeSession_CreatesValidSession()
    {
        // Test session initialization
    }

    [Fact]
    public void SessionSettings_DefaultValues_AreCorrect()
    {
        // Test default configuration values
    }
}
```

---

### 3. Blazor Component Tests (Optional)

**Location**: `Karamel.Web.Tests/Pages/HomePageTests.cs` (Future)  
**Framework**: bUnit

#### Purpose
Test Home.razor component behavior and user interactions.

#### Setup Required

1. Create test project:
   ```bash
   dotnet new xunit -n Karamel.Web.Tests
   dotnet add Karamel.Web.Tests reference Karamel.Web
   dotnet add Karamel.Web.Tests package bUnit
   dotnet add Karamel.Web.Tests package Moq
   ```

2. Configure bUnit test context with Fluxor

#### Test Cases

```csharp
public class HomePageTests : TestContext
{
    [Fact]
    public void StartButton_IsDisabled_WhenLibraryNotSelected()
    {
        // Arrange
        var mockJS = Services.AddMockJSRuntime();
        Services.AddFluxor(/* ... */);
        
        // Act
        var component = RenderComponent<Home>();
        
        // Assert
        var startButton = component.Find("button.start-session");
        Assert.True(startButton.HasAttribute("disabled"));
    }

    [Fact]
    public void SelectLibrary_CallsJavaScriptInterop()
    {
        // Arrange
        var mockJS = Services.AddMockJSRuntime();
        var component = RenderComponent<Home>();
        
        // Act
        var selectButton = component.Find("button.select-library");
        selectButton.Click();
        
        // Assert
        mockJS.VerifyInvoke("pickLibraryDirectory");
    }

    [Fact]
    public void StartSession_OpensNewTabs_AndNavigates()
    {
        // Test that window.open is called and navigation occurs
    }

    [Fact]
    public void RequireSingerName_Checkbox_TogglesCorrectly()
    {
        // Test checkbox state management
    }
}
```

---

### 4. Manual/E2E Testing Scenarios

Some features require full browser context and cannot be easily unit tested.

#### Critical Scenarios

**Session Initialization Flow:**
1. Open `/` in browser (Chrome/Edge)
2. Verify browser compatibility message is NOT shown
3. Verify "Start Karaoke Session" button is disabled
4. Click "Select Karaoke Library"
5. Verify File System Access API permission dialog appears
6. Select a folder with MP3+CDG files
7. Verify button becomes enabled
8. Verify library path is displayed

**Settings Configuration:**
1. Toggle "Require singer name" checkbox (verify state)
2. Toggle "Allow singers to reorder playlist" checkbox (verify state)
3. Change "Pause between songs" to invalid value (verify validation)
4. Change filename pattern to invalid format (verify validation)
5. Verify defaults are restored correctly

**Session Start:**
1. Click "Start Karaoke Session"
2. Verify two new tabs/windows open:
   - `/playlist?session={guid}`
   - `/singer?session={guid}`
3. Verify current tab navigates to `/nextsong?session={guid}`
4. Verify all tabs have same session GUID in URL
5. Verify main tab retains directory access (can still load files)

**Browser Compatibility:**
1. Open in Firefox (no File System Access API support)
2. Verify warning message is displayed
3. Verify "Select Library" button is disabled or shows error

**Multiple Sessions:**
1. Start session A in tab 1
2. Open new tab, start session B
3. Verify sessions are isolated (different GUIDs)
4. Verify session A tabs only communicate with session A
5. Verify session B tabs only communicate with session B

---

## Testing Priorities

### High Priority (Must Have)
1. ✅ JavaScript unit tests for existing modules (fileAccess, metadata, sessionBridge)
2. ✅ JavaScript unit tests for homeInterop module (Step 2.5)
3. ✅ C# Fluxor reducer signature tests (prevents runtime errors)
4. ⬜ Manual testing of File System Access API
5. ⬜ Manual testing of multi-tab session flow

### Medium Priority (Should Have)
1. ⬜ C# unit tests for state management behavior (Session, Library, Playlist reducers)
2. ⬜ Validation tests for user input
3. ⬜ Manual testing of multiple concurrent sessions

### Low Priority (Nice to Have)
1. ⬜ bUnit component tests for Blazor pages
2. ⬜ E2E tests with Playwright/Selenium
3. ⬜ Performance tests for large libraries (1000+ songs)
4. ⬜ Accessibility tests (a11y)

---

## Running Tests

### C# Tests

```bash
# Run all C# tests
dotnet test

# Run with detailed output
dotnet test --verbosity normal

# Run specific test class
dotnet test --filter FluxorReducerSignatureTests

# Run with coverage (requires coverlet.msbuild package)
dotnet test /p:CollectCoverage=true
```

### JavaScript Tests

```bash
# Run all JavaScript tests in watch mode
npm test

# Run tests once (CI mode)
npm run test:run

# Run tests with UI
npm run test:ui

# Run specific test file
npm test -- fileAccess.test.js

# Run with coverage
npm test -- --coverage
```

---

## Coverage Goals

### Phase 2 (Current)
- **JavaScript modules**: ✅ 80%+ coverage achieved (107 tests passing)
- **Critical paths**: ✅ 100% coverage (session init, file access, state sync)
- **C# Fluxor reducers**: ✅ 100% signature validation (7 tests passing)
- **C# state logic**: Basic validation (inline, no behavior tests yet)

### Phase 3+ (Future)
- **C# business logic**: 80%+ coverage
- **Blazor components**: 60%+ coverage (focus on critical interactions)
- **E2E tests**: Cover happy path + critical error scenarios

---

## Test Data

### Sample Directory Structure for Testing

```
TestLibrary/
  Artist1/
    Song1.mp3
    Song1.cdg
    Song2.mp3
    Song2.cdg
  Artist2/
    Song3.mp3
    Song3.cdg
    NoMatchingMp3.cdg
    NoMatchingCdg.mp3
```

### Sample Session Configuration

```json
{
  "sessionId": "550e8400-e29b-41d4-a716-446655440000",
  "requireSingerName": true,
  "pauseBetweenSongs": 5,
  "filenamePattern": "%artist - %title",
  "libraryPath": "TestLibrary"
}
```

### Sample Songs

```javascript
[
  {
    id: "guid-1",
    artist: "Test Artist 1",
    title: "Test Song 1",
    mp3FileName: "Test Artist 1 - Test Song 1.mp3",
    cdgFileName: "Test Artist 1 - Test Song 1.cdg"
  },
  // ... more songs
]
```

---

## Continuous Integration

### Recommended CI Setup (Future)

```yaml
# .github/workflows/test.yml
name: Run Tests

on: [push, pull_request]

jobs:
  test-js:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - uses: actions/setup-node@v3
      - run: npm install
      - run: npm run test:run
      
  test-csharp:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - uses: actions/setup-dotnet@v3
      - run: dotnet test
```

---

## Best Practices

1. **Write tests first** for new modules (TDD approach)
2. **Keep tests fast** - mock expensive operations
3. **Test behavior, not implementation** - focus on public APIs
4. **Use descriptive test names** - explain what and why
5. **Arrange-Act-Assert** pattern for clarity
6. **One assertion per test** when possible
7. **Clean up side effects** in afterEach hooks
8. **Mock external dependencies** (APIs, browser features, file system)
9. **Test edge cases** (empty inputs, null, undefined, errors)
10. **Update tests when behavior changes** - keep them in sync

---

**Last Updated**: December 29, 2025
