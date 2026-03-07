# Research: Remove Deprecated Methods and Properties

**Date**: 2026-03-07  
**Feature**: Remove deprecated code (LinkToken, BroadcastPlaylistUpdatedAsync)  
**Status**: Complete (no unknowns requiring investigation)

## Research Questions & Findings

### 1. EF Core Migration Strategy for Column Removal

**Question**: What is the safest way to remove the `LinkToken` column from the `Sessions` table without data loss?

**Decision**: Use a two-phase migration approach:
1. **Data migration**: Copy existing `LinkToken` values to `AdminToken` (if any sessions exist with LinkToken but not AdminToken)
2. **Schema migration**: Drop the `LinkToken` column after data is migrated

**Rationale**:
- EF Core `DropColumn` operation is straightforward but requires ensuring no data loss
- The spec (FR-005) explicitly requires copying LinkToken → AdminToken before dropping the column
- Both SQL Server and SQLite support `ALTER TABLE DROP COLUMN`
- Migration can be tested locally with SQLite before deploying to Azure SQL

**Alternatives considered**:
- **Single-phase migration**: Drop column without data copy → Rejected due to potential data loss if any legacy sessions exist
- **Keep column with null values**: Mark as deprecated → Rejected because goal is complete cleanup

**Implementation**:
```csharp
// In the migration Up() method:
migrationBuilder.Sql("UPDATE Sessions SET AdminToken = LinkToken WHERE AdminToken IS NULL AND LinkToken IS NOT NULL");
migrationBuilder.DropColumn(name: "LinkToken", table: "Sessions");

// In the migration Down() method (rollback):
migrationBuilder.AddColumn<string>(name: "LinkToken", table: "Sessions", nullable: true);
migrationBuilder.Sql("UPDATE Sessions SET LinkToken = AdminToken");
```

---

### 2. Verification Strategy for Complete Removal

**Question**: How to ensure no code references remain after deleting deprecated methods/properties?

**Decision**: Use multi-layered verification:
1. **Compile-time check**: `dotnet build` must pass with zero errors/warnings (FR-001)
2. **Test suite validation**: All tests must pass (FR-002, FR-014)
3. **Code search verification**: Search for "LinkToken", "linkToken", "BroadcastPlaylistUpdatedAsync" across solution (SC-008)
4. **Manual review**: Check XML docs and log messages for deprecated term references (FR-012, FR-013)

**Rationale**:
- Compiler catches most removal issues (broken call sites, missing interface implementations)
- Tests catch runtime issues (e.g., SignalR authorization still works after LinkTokenHubFilter removal)
- Code search catches documentation/comment leftovers that compiler ignores
- Manual review ensures log messages are updated to reflect new terminology

**Alternatives considered**:
- **Static analysis tools**: ReSharper, SonarQube → Not needed, built-in compiler + grep search sufficient for this cleanup
- **Deprecation warnings first**: Mark as `[Obsolete]` then remove → Rejected because code is already documented as deprecated

---

### 3. Backward Compatibility for API Responses

**Question**: What if existing clients expect the `linkToken` field in session creation responses?

**Decision**: No backward compatibility required - direct removal is safe

**Rationale**:
- The spec's Edge Cases section explicitly states: "Backward compatibility not required" because:
  - LinkToken was already deprecated (documented as such in code comments)
  - LinkToken is functionally identical to AdminToken (same HMAC-SHA256 generation)
  - No external systems depend on the linkToken field (Karamel-Web is a standalone application)
  - Frontend already uses AdminToken/SingerToken exclusively (LinkToken parameters are optional and unused)
- The backend API is not versioned (no `/api/v1/sessions` structure), so clients expect the latest schema

**Alternatives considered**:
- **Dual response fields**: Return both `linkToken` and `adminToken` for a transition period → Rejected because adds complexity without benefit (no known clients depending on linkToken)
- **API versioning**: Create `/api/v2/sessions` without linkToken → Rejected as overkill for internal cleanup

---

### 4. SignalR Authorization After LinkTokenHubFilter Removal

**Question**: Will SignalR authorization still work after removing `LinkTokenHubFilter`?

**Decision**: Yes, authorization is already handled by AdminToken/SingerToken validation in `PlaylistHub` methods

**Rationale**:
- Current authorization flow (post-LinkToken refactor):
  1. Client sends `adminToken` or `singerToken` in SignalR connection headers
  2. `PlaylistHub` methods validate tokens using `ITokenService.ValidateAdminToken()` / `ValidateSingerToken()`
  3. `LinkTokenHubFilter` is a leftover from the legacy implementation and does nothing (or only validates a token type that's no longer sent)
- Removing `LinkTokenHubFilter` registration from `Program.cs` has no impact because the actual authorization logic is in the hub methods themselves

**Alternatives considered**:
- **Keep LinkTokenHubFilter for legacy support**: → Rejected because there are no legacy clients (LinkToken was already replaced in all frontend code)
- **Replace LinkTokenHubFilter with AdminTokenHubFilter**: → Rejected because hub methods already handle token validation explicitly

---

## Technology Choices

### EF Core Migrations

**Best practices followed**:
- Use `migrationBuilder.Sql()` for data transformations (copy LinkToken → AdminToken)
- Use `migrationBuilder.DropColumn()` for schema changes
- Provide rollback logic in `Down()` method for safe migration reversal
- Test migration locally with SQLite before deploying to Azure SQL Server
- Follow guidance from [database-migrations.instructions.md](.github/instructions/database-migrations.instructions.md)

### xUnit Test Strategy

**Best practices followed**:
- Run targeted tests first when modifying a single file (`dotnet test --filter ClassName`)
- Run full test suite after all changes to catch integration issues
- No new tests required for pure deletion (existing tests validate no regressions)
- C# tests use xUnit assertions (not FluentAssertions, which is not installed)

---

## Summary

All research questions resolved without external investigation. Key findings:
1. **EF Core migration**: Two-phase approach (data copy → column drop) with rollback support
2. **Verification**: Multi-layered (compiler + tests + code search + manual review)
3. **Backward compatibility**: Not required - direct removal is safe
4. **SignalR authorization**: Already handled by AdminToken/SingerToken - LinkTokenHubFilter is dead code

No unknowns remain. Ready to proceed to Phase 1 (Design & Contracts).
