# Strict Session Validation Implementation Plan

## Objective
Implement strict session GUID validation for multi-session support where all views (except Home) MUST have a matching session GUID in the URL query parameter.

## Current State
- Session validation is lenient: if SessionParam is missing but session exists in state, it's allowed
- ~80 tests across 5 test files don't provide session parameters in NavigationManager URLs
- Tests rely on local helper methods that don't configure proper URLs

## Target State
- Strict validation: SessionParam REQUIRED + must be valid GUID + must match CurrentSession.SessionId
- All tests properly configure NavigationManager with session parameter
- Centralized test infrastructure for easy maintenance

---

## Implementation Steps

### Phase 1: Test Infrastructure (Foundation)

#### 1.1 Create SessionTestBase.cs
**File**: `Karamel.Web.Tests/SessionTestBase.cs`

Create abstract base class with:
- `SetupTestWithSession()` - Auto-constructs URL with session parameter
- `SetupFluxorWithStates()` - Flexible setup with custom URI
- `CreateTestSession()` - Helper to create test sessions
- `CreateTestSong()` - Helper to create test songs
- `FakeNavigationManager` - Reusable navigation manager for tests

**Benefits**:
- Single source of truth for test setup
- Reduces code duplication across 5 test files
- Makes future test maintenance easier

#### 1.2 Create Integration Test Examples
**File**: `Karamel.Web.Tests/SessionIntegrationTests.cs`

Demonstrate integration testing patterns:
- Real Fluxor store with actual state management
- Multi-session isolation testing
- State transition verification
- Session lifecycle testing

---

### Phase 2: Update View Validation (Make Strict)

#### 2.1 Update NextSongView.razor Validation
Change `ValidateSession()` to:
```csharp
private bool ValidateSession()
{
    // Session parameter is REQUIRED
    if (string.IsNullOrWhiteSpace(SessionParam) || 
        !Guid.TryParse(SessionParam, out var sessionGuid))
    {
        return false;
    }

    // Session must exist in state
    if (SessionState.Value.CurrentSession == null)
    {
        return false;
    }

    // Session ID must match the parameter
    return SessionState.Value.CurrentSession.SessionId == sessionGuid;
}
```

#### 2.2 Update PlayerView.razor Validation
Same strict validation pattern as NextSongView

#### 2.3 Update Playlist.razor Validation
Same strict validation pattern

#### 2.4 Update SingerView.razor Validation
Same strict validation pattern

**Impact**: All ~80 tests will fail until updated

---

### Phase 3: Update Test Files (One by One)

#### 3.1 Update NextSongViewTests.cs (19 tests)
**Changes**:
1. Inherit from `SessionTestBase` instead of `TestContext`
2. Remove local `SetupFluxor()` method
3. Replace all calls with `SetupTestWithSession(sessionState, playlistState, view: "nextsong")`
4. Run tests, verify all pass

**Pattern**:
```csharp
// Before:
SetupFluxor(sessionState, playlistState);

// After:
SetupTestWithSession(sessionState, playlistState, view: "nextsong");
```

#### 3.2 Update PlayerViewTests.cs (21 tests)
Same pattern as NextSongViewTests

#### 3.3 Update PlaylistPageTests.cs (14 tests)
Same pattern, use `view: "playlist"`

#### 3.4 Update SingerViewTests.cs (22 tests)
Same pattern, use `view: "singer"` and include `LibraryState`

#### 3.5 Update NextSongViewIntegrationTests.cs (4 tests)
Different approach - uses real Fluxor:
1. Keep real Fluxor setup
2. Add NavigationManager configuration with session parameter
3. Extract session ID from store state after dispatching LoadSessionAction

---

### Phase 4: Verification & Documentation

#### 4.1 Run Full Test Suite
Execute `dotnet test` and verify all 91+ tests pass

#### 4.2 Update NavigationFlowTests
Verify existing tests still pass (should already be strict)

#### 4.3 Update DEVELOPMENT_PLAN.md
Document the strict validation approach in Step 2.11

#### 4.4 Commit Changes
Commit with message describing strict session validation implementation

---

## Testing Strategy

### Unit Tests
- Mock all dependencies
- Test component logic in isolation
- Fast execution, high coverage

### Component Integration Tests
- Use real Fluxor store
- Mock external dependencies (JSRuntime, BroadcastChannel)
- Test state transitions and component reactions
- Verify session isolation

### Test Coverage Goals
- All session validation paths (valid, invalid, missing, mismatched)
- Multi-session scenarios
- State transition flows
- Error handling

---

## Risk Mitigation

### Risk 1: Many Tests Failing Simultaneously
**Mitigation**: Update one test file at a time, verify tests pass before moving to next

### Risk 2: Complex Test Dependencies
**Mitigation**: SessionTestBase provides centralized helpers, reducing complexity

### Risk 3: Integration Test Complexity
**Mitigation**: Provide clear examples in SessionIntegrationTests.cs

### Risk 4: Missed Test Cases
**Mitigation**: Run full test suite after each file update

---

## Success Criteria

✅ All 91+ tests pass with strict validation
✅ SessionTestBase reduces test code duplication
✅ Integration tests demonstrate proper patterns
✅ Session validation is strict and secure
✅ Multi-session support is properly validated
✅ Documentation is updated

---

## Timeline Estimate
- Phase 1 (Infrastructure): 15-20 minutes
- Phase 2 (Validation): 10 minutes
- Phase 3 (Test Updates): 30-40 minutes (per file verification)
- Phase 4 (Verification): 10 minutes
- **Total**: ~75-90 minutes

---

## Notes
- Keep NavigationFlowTests as reference implementation
- Document any edge cases discovered during implementation
- Consider adding helper methods to SessionTestBase as needed
