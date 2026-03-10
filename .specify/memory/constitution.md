<!--
SYNC IMPACT REPORT
==================
Version change: 1.1.0 → 1.1.1 (PATCH — factual corrections only)

Changed (v1.1.1):
  - Principle V: corrected test counts (101 → 260+, 3 skipped → 9 skipped)
  - Principle VI: fixed broken logging reference (instructions → prompts)
  - copilot-instructions.md: same reference fix
  - playlist-status-system.instructions.md: same reference fix

Previously added (v1.1.0):
  - VII. Domain-Driven Design (new)
Previously added (v1.0.0):
  - I. Multi-Session, Multi-Device Architecture
  - II. Privacy by Design & GDPR Compliance
  - III. Clean Code & C# Conventions
  - IV. JavaScript↔C# Serialization Integrity
  - V. Quality Gates & Testing Discipline
  - VI. Structured Observability
  - Additional Constraints: License & Attribution
  - Additional Constraints: Azure Deployment
  - Additional Constraints: Playlist Status System
  - Additional Constraints: Database Migrations
  - Development Workflow
  - Governance

Removed sections: none
Templates updated: none required
Deferred TODOs: none
-->

# Karamel-Web Constitution

## Core Principles

### I. Multi-Session, Multi-Device Architecture (NON-NEGOTIABLE)

Karamel-Web MUST support fully independent, concurrent karaoke sessions across different browser
tabs **and** across different physical devices simultaneously. Each session is identified by a
unique GUID passed via the `?session={guid}` query parameter.

Rules that MUST NOT be violated:

- **Session ID originates from the backend.** The backend creates the session and generates a link
  token (HMAC-SHA256 of the session ID). Any client-invented session ID causes 401 failures on
  every authenticated request.
- **Never assume secondary tabs are on the same device.** SingerView and Playlist pages are
  routinely opened on phones, tablets, and other computers via QR code. `sessionStorage` and
  `BroadcastChannel` are same-device primitives and MUST NOT be used as the primary data source
  for any page that could run on a remote device.
- **Backend API is the source of truth** for library data, playlist state, and session config on
  all non-main tabs. `sessionStorage` is an optimisation for same-device tab restoration only.
- **One session = one playlist.** `playlistId` equals `sessionId` at all times; there is no
  separate playlist creation or lookup step.
- **All pages** except `Home.razor` MUST validate the `?session=` query parameter against
  `SessionState.Value.CurrentSession.SessionId` and show an error if they do not match.
- File paths (Mp3FileName, CdgFileName, etc.) MUST NEVER be transmitted outside the main tab. The
  backend stores no file paths.

Guidance: [copilot-instructions.md](.github/copilot-instructions.md) — *Multi-Session
Architecture* and *Privacy Architecture* sections.

### II. Privacy by Design & GDPR Compliance

Karamel-Web processes locally-owned media files. User privacy is non-negotiable.

- **File paths never leave the main tab.** Upload DTOs carry only Artist, Title, and safe metadata
  (duration, genre, album). `MetadataJson` MUST NOT contain file paths.
- **No personal data is persisted on the backend** beyond session coordination (session ID, link
  token, library metadata, playlist items). Sessions expire after a 30-minute TTL.
- **Consent gate for telemetry.** Application Insights JavaScript SDK is initialised only after the
  user accepts the consent banner (`consentBanner.js`). Telemetry MUST NOT fire before consent.
- **Never log sensitive data.** Passwords, full tokens, API keys, and personally identifiable
  information MUST NOT appear in any log output (structured or otherwise), on either the frontend
  or the backend.
- **Minimal data retention.** Session and playlist data are cleaned up automatically on TTL expiry.
  No long-term user profiling data is stored.

GDPR legal basis: legitimate interest for session coordination; explicit consent for telemetry.

### III. Clean Code & C# Conventions

All C# code in `Karamel.Backend` and `Karamel.Web` MUST:

- Target the latest released C# version (currently **C# 14**) and .NET 10.
- Use **structured logging** via `ILogger<T>` with named parameters — never string interpolation
  in log messages (`_logger.LogInformation("Msg {Param}", value)` not `$"Msg {value}"`).
- Follow PascalCase for public members, camelCase for private fields and local variables.
- Prefix interface names with `I` (e.g., `ISessionApiClient`).
- Declare variables non-nullable where possible; use `is null` / `is not null` for null checks;
  never add redundant null checks when the type system guarantees non-null.
- Use file-scoped namespace declarations and pattern matching / switch expressions where
  appropriate.
- Provide XML doc comments on all public APIs.
- Implement the **Repository pattern** for all data access; keep controllers thin; put business
  logic in services or Fluxor Effects (never in Razor components or repositories).
- Services MUST be stateless helpers. Fluxor Effects are the sole orchestrators of state
  mutations; services MUST NOT dispatch Fluxor actions.

JavaScript modules in `wwwroot/js/` MUST:

- Be ES modules with explicit named exports.
- Create a per-module logger via `createLogger('ModuleName')` from `logger.js` — no bare
  `console.log` calls in production paths.
- Match function parameter names and order exactly with the C# `InvokeAsync` call sites (parameter
  count and order form a strict contract).

Detailed C# guidance: [csharp.instructions.md](.github/instructions/csharp.instructions.md)

### IV. JavaScript↔C# Serialization Integrity

Every DTO that crosses the JavaScript↔C# boundary MUST:

- Use `[JsonPropertyName("camelCase")]` attributes on all properties — PascalCase defaults are
  forbidden.
- Serialise enums as **strings**, never integers (e.g., `"mp3cdg"` not `1`).
- Provide both `ConvertDtoToSong` and `ConvertSongToDto` helpers.
- Be covered by a round-trip serialisation test verifying that JavaScript → C# → JavaScript
  preserves all properties without data loss.

Detailed guidance:
[serialization.instructions.md](.github/instructions/serialization.instructions.md)

### V. Quality Gates & Testing Discipline

No code is merged without passing all quality gates:

- `dotnet build` MUST produce zero errors and zero warnings before every commit.
- `dotnet test Karamel.Web.Tests` MUST yield ≥ 260 passing tests (9 skipped by design).
- `npm run test:run` (in `Karamel.Web/wwwroot`) MUST yield zero failures across all JS tests.
- **Targeted test runs first**: when changing a single file, run only the affected test file;
  run the full suite only after the targeted test passes.
- Tests are written **alongside or before** production code — never deferred to a later PR.
- C# tests use xUnit assertions exclusively. FluentAssertions MUST NOT be used (not installed).
- Test methods MUST NOT contain "Arrange / Act / Assert" comments.
- Backend integration tests (`dotnet test Karamel.Backend.Tests`) MUST be requested from the user
  before merging backend changes (~40 s run time; not auto-run by the agent).

Testing strategy: [TESTING_STRATEGY.md](TESTING_STRATEGY.md)

### VI. Structured Observability

Every significant code path MUST be observable in production via Application Insights.

- **Backend**: Structured `ILogger<T>` at the correct level: Information → normal flow,
  Warning → recoverable errors / auth issues, Error → exceptions with full context.
- **Frontend JS**: `createLogger('ModuleName')` from `logger.js`; warnings and errors are
  automatically forwarded to Application Insights as custom events / exceptions.
- **Blazor components**: Wrap debug output in `#if DEBUG`; rely on JavaScript logger and
  backend `ILogger<T>` for production telemetry — no `Console.WriteLine` in production builds.
- **Log level discipline**: In production `window.logLevel = 2`; only Warn and Error reach
  Application Insights. Debug/Info appear only in development console.
- Sensitive data MUST NOT appear in any log output (see Principle II).

Full guidance:
[logging-observability.prompt.md](.github/prompts/logging-observability.prompt.md)

### VII. Domain-Driven Design

Karamel-Web organises code around the **domain concepts** of karaoke management — Sessions,
Playlists, Songs, Singers — not around technical layers alone.

- **Ubiquitous language**: Use domain terms consistently across code, tests, documentation, and
  conversation. `Session`, `PlaylistItem`, `SongStatus`, `Singer` are the canonical names; do not
  introduce synonyms (e.g., ~~`Queue entry`~~ for `PlaylistItem`).
- **Bounded contexts**: The backend (`Karamel.Backend`) owns persistence and business rules;
  the frontend (`Karamel.Web`) owns presentation and local file access. Cross-context
  communication happens exclusively through the published API (REST + SignalR) and DTOs, never
  through shared internal models.
- **Rich domain models over anemic models**: Business rules belong in domain/service classes,
  not in controllers or Razor components. Repositories expose aggregate roots (`Playlist`,
  `Session`) and hide query details from callers.
- **Aggregates and invariants**: `Playlist` is the aggregate root for playlist items.
  Mutations (add, remove, reorder, status change) MUST go through `PlaylistHub` methods, never
  by directly updating `PlaylistItem` state on the frontend. This preserves server-enforced
  invariants (e.g., auto-promotion of UpNext).
- **YAGNI & bounded complexity**: Introduce DDD building blocks (Value Objects, Domain Events,
  etc.) only when they reduce complexity or enforce an invariant — not speculatively.
- **Feature folders**: Group files by feature/domain concept (e.g., `Store/Playlist/`,
  `Controllers/`, `Repositories/`) rather than exclusively by technical type.

## Additional Constraints

### License & Attribution

Karamel-Web is released under the **MIT License** (© 2025 Dominik Damerow). See
[LICENSE](LICENSE).

- The MIT license text and copyright notice MUST be preserved in all distributions and forks.
- All third-party libraries have their own licenses (ISC, LGPL-3.0, MIT). Attribution MUST be
  maintained in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) whenever a new dependency is
  added.
- No dependency with a license incompatible with MIT (e.g., GPL-2.0 without classpath exception)
  may be added without an explicit decision recorded in an ADR under `docs/`.

### Azure Deployment

- There is **no development Azure environment**. All deployments MUST target resource group
  `rg-karamel-prod`. Deployment to `rg-karamel-dev` is strictly prohibited.
- Resource naming convention: `rg-karamel-prod-{resourceType}` (e.g., `rg-karamel-prod-api`).
- Use **Azure Cloud Shell (Bash)** for database and resource configuration tasks; SSH is not
  recommended.

Full guidance:
[azure-deployment.instructions.md](.github/instructions/azure-deployment.instructions.md),
[azure-resource-configuration.instructions.md](.github/instructions/azure-resource-configuration.instructions.md)

### Playlist Status System

The playlist status lifecycle (`Queued → UpNext → NowPlaying → Completed`) is enforced by the
backend and MUST NOT be reimplemented on the frontend. Auto-promotion from Queued to UpNext
happens server-side after every playlist mutation. Frontend code dispatches
`AdvanceToNextSongAction` only; it MUST NOT manually set UpNext status.

Detailed rules:
[playlist-status-system.instructions.md](.github/instructions/playlist-status-system.instructions.md)

### Database Migrations

Migrations are provider-specific. SQL Server migrations are used in production; SQLite migrations
are for local development. Migration sets MUST NOT be mixed. Set `$env:DB_PROVIDER` before
running `dotnet ef migrations add`.

Guidance:
[database-migrations.instructions.md](.github/instructions/database-migrations.instructions.md)

## Development Workflow

- **Branch protection**: NEVER commit directly to `main`, `develop`, or `master`. All work
  happens on `feature/descriptive-name` branches.
- **Commit messages** are user-facing outcome descriptions (e.g., `Add Azure deployment pipeline`),
  not internal task references (e.g., ~~`Implement Phase 3`~~).
- **Push only when explicitly requested** by the user or required by a CI/CD context.
- `plan*.md` and `plan-*.md` files are git-ignored working notes and MUST NEVER be staged or
  committed.
- Before any change: verify the current branch (`git branch`), run `dotnet build`, run the
  relevant test suite to establish a passing baseline.
- After any change: run build → targeted tests → full test suite → manual UI verification if the
  change affects UI.

Full rules: [git-workflow.instructions.md](.github/instructions/git-workflow.instructions.md)

## Governance

This constitution supersedes all other development practices for Karamel-Web. In the event of a
conflict between this document and any other guideline, the constitution takes precedence (unless
a more specific instruction file provides narrower, non-conflicting detail for its domain).

**Amendment procedure**:

1. Create a `feature/amend-constitution-vX.Y.Z` branch.
2. Edit this file, increment the version per the semver policy below, update `LAST_AMENDED_DATE`,
   and update the Sync Impact Report comment at the top.
3. Propagate changes to dependent templates (plan, spec, tasks) as noted in the Sync Impact
   Report.
4. Submit a pull request; merge only after at least one reviewer approves.

**Versioning policy**:

- MAJOR — backward-incompatible removal or redefinition of a principle.
- MINOR — new principle or section added, or materially expanded guidance.
- PATCH — clarifications, wording refinements, typo fixes.

**Compliance review**: All pull requests MUST be verified against Principles I–VII before merge.
For AI-assisted work, the agent MUST reference this constitution when planning and implementing.
Use [copilot-instructions.md](.github/copilot-instructions.md) as the runtime development
reference; this constitution governs *what* is non-negotiable, the instructions govern *how*.

---

**Version**: 1.1.1 | **Ratified**: 2026-02-21 | **Last Amended**: 2026-03-10
