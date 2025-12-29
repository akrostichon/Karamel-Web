# Karamel.Web.Tests

Unit tests for the Karamel-Web Blazor application.

## Running Tests

```bash
# Run all tests
dotnet test

# Run with detailed output
dotnet test --verbosity normal

# Run specific test class
dotnet test --filter FluxorReducerSignatureTests
```

## Test Categories

### Fluxor Reducer Signature Tests ✅

**File**: `FluxorReducerSignatureTests.cs`

These tests validate that all Fluxor reducers follow the correct signature pattern using reflection. They catch configuration errors at test time instead of runtime.

**What they check:**
- All `[ReducerMethod]` methods have exactly 2 parameters
- First parameter is a State type (ends with "State")
- Second parameter is an Action type (ends with "Action")
- All reducer methods are static
- Return type is a State type
- Input state type matches return state type
- All reducer classes are static

**Example error caught:**
```
❌ LibraryReducers.ReduceLoadLibraryAction has 1 parameters (expected 2)
```

This prevents cryptic runtime errors like:
```
AggregateException: Method must have 2 parameters (state, action) when [ReducerMethodAttribute]...
```

## Future Tests

- State management behavior tests
- Action validation tests
- bUnit component tests
- Integration tests

## Test Coverage

Run tests before committing changes:
```bash
dotnet test
```

All tests should pass before creating a pull request.
