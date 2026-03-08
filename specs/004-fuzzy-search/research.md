# Research: Smart Search — Fuzzy Matching, Relevance Ranking, and Spelling Suggestions

**Phase 0 output for**: `specs/004-fuzzy-search/plan.md`  
**Date**: 2026-03-08

---

## R1 — Fuzzy Algorithm Selection

**Question**: Which string-distance algorithm provides the best typo-tolerance
for song titles and artist names within 1–2 character edits?

**Decision**: **Restricted Edit Distance (Optimal String Alignment — OSA)**,
a practical variant of Damerau-Levenshtein that counts transpositions as a
single edit.

**Rationale**:
- The spec acceptance scenario "Micheal" → "Michael" is a transposition of
  adjacent characters ('h'↔'e'). Standard Levenshtein counts it as 2 edits
  (delete + insert), missing it at the threshold of 2 when combined with any
  other character difference. OSA counts it as 1 edit.
- OSA is simpler to implement than full Damerau-Levenshtein (no sub-string
  transpositions needed for single-word singer input).
- No NuGet package required; OSA can be implemented in ~30 lines of C#
  using a two-row rolling array, keeping allocations minimal.
- Time complexity: O(m·n) per comparison; for song title strings of average
  length 25 characters the comparison is ≈625 integer operations — negligible
  per-song cost.

**Alternatives considered**:
- Standard Levenshtein — rejected because transpositions (by far the most
  common typo type) cost 2 edits instead of 1, causing missed matches.
- Soundex / DIFFERENCE() — rejected because (a) SQLite does not support
  SOUNDEX natively, (b) phonetic matching is too coarse for non-English
  artist names, and (c) it would add a SQL Server-only code path.
- Trigram similarity (pg_trgm style) — rejected because it requires the
  full library to be tokenised into trigrams and stored, adding schema
  complexity. The project has no trigram-capable DB extension.
- External NuGet packages (e.g., `FuzzySharp`) — rejected by the spec
  constraint "no new NuGet packages". Also unnecessary given algorithm
  simplicity.

---

## R2 — Candidate Bounding Strategy

**Question**: When zero exact/substring matches exist, how do we limit the set
of songs that undergo Levenshtein scoring without loading the entire library
into memory?

**Decision**: **Two-phase search with first-token prefix pre-filter**:

1. **Phase 1 — SQL LIKE** (fast path, indexed): query the DB with
   `WHERE Artist LIKE '%q%' OR Title LIKE '%q%'`. Returns exact,
   prefix, and substring matches. If any results → score and return.
2. **Phase 2 — Bounded fuzzy scan** (fallback, only when Phase 1 = 0):
   - Extract the first character of each whitespace-delimited token in
     `q` (e.g., `"raps"` → `'r'`; `"mic jac"` → `['m','j']`).
   - Query: `WHERE (Artist LIKE 'r%' OR Title LIKE 'r%')` (per token,
     combined with OR).
   - Compute OSA distance for each candidate in C#.
   - Keep candidates with `distance ≤ threshold` (threshold = 2 for
     query length ≥ 6, threshold = 1 for query length 3–5).
3. **Phase 3 — Suggestion generation** (only when Phase 2 also = 0):
   - Fetch up to 300 songs ordered alphabetically.
   - Tokenise Artist and Title into individual words.
   - Compute OSA distance from `q` to every token.
   - Return the 3 tokens with the smallest normalised distance
     (`distance / max(len_q, len_token)`), deduplicated by text.

**Performance analysis** (3,000 songs, 50-char average field):
- Phase 1: Single indexed LIKE query — ~5–20 ms.
- Phase 2 worst case (all songs share first letter 'a'): ~1,200 songs
  × 2 comparisons × 50² ops = ~6M integer ops ≈ 2–5 ms in C# JIT.
- Phase 3 (suggestion scan): 300 songs × avg 4 tokens × 50² ops ≈ 3M
  ops ≈ 1–3 ms. Well within 800 ms budget.

**Alternatives considered**:
- Load entire library into memory — rejected for sessions with large
  libraries (5,000 hard cap × two string fields × avg 50 bytes = ~500 KB
  per query allocation is acceptable but unnecessary when prefix filtering
  achieves the same result with 10–40× fewer candidates).
- BK-tree index at session startup — rejected because sessions are
  short-lived (30-min TTL) and the build cost (~50 ms for 3,000 nodes) is
  unjustified for an occasional fallback code path.

---

## R3 — Relevance Tier Ordering Across Paginated Loads

**Question**: How can relevance ordering (`ExactTitle > PartialTitle >
ArtistOnly > FuzzyMatch`) be preserved across "Load More" paginated requests?

**Decision**: **In-memory sort with skip/take applied after scoring**.

When a `search` query is present:
1. Phase 1 (SQL LIKE) returns **all matching songs** for that query (not
   a single page). The result set is capped at **500 records** to bound
   memory.
2. All candidates are scored in C# and sorted by `(Tier, Artist, Title)`.
3. Pagination (`Skip((page-1)*pageSize).Take(pageSize)`) is applied in C#
   after scoring.
4. `TotalCount` = scored list count (≤ 500 cap → correct within the cap).
5. For queries returning > 500 candidates the cap is documented; this only
   happens for extremely short or common queries (e.g., single common word
   in a 5,000-song library) where the first 500 relevance-ordered results
   are the meaningful ones.

Without a search query (browse): DB-level pagination (ORDER BY Artist, Title
+ Skip/Take) is unchanged.

**Alternatives considered**:
- DB-level ORDER BY with a computed tier column — rejected because tier
  classification requires application-level string comparison logic that
  cannot be expressed in a portable EF LINQ-to-SQL expression for both
  SQLite and SQL Server.
- Sorting only within the current page — rejected because FR-007 explicitly
  requires relevance ordering to be preserved across pages.
- Storing relevance scores in a temporary table — rejected because it adds
  schema complexity and is incompatible with session-scoped, ephemeral data.

---

## R4 — REST Response Contract Extensibility

**Question**: How should `SearchSuggestion` items be added to the API
response without breaking existing clients that parse `items[]` and
`X-Total-Count`?

**Decision**: **Replace the current array response body with a JSON object**
that includes `items`, `totalCount`, and `suggestions`.

Current REST format:
- Body: `[{...}, {...}]` (plain array)
- Header: `X-Total-Count: 42`

New REST format:
- Body: `{ "items": [...], "totalCount": 42, "suggestions": [] }`
- Header: `X-Total-Count` retained for backwards compatibility

The JavaScript bridge (`signalRBridge.js`) already wraps the REST response
array into `{ items, page, pageSize, totalCount }`. With the new format the
bridge reads `data.items` and `data.totalCount` directly from the body
(same field names). The `X-Total-Count` header becomes redundant but is
retained for any curl/Postman consumers.

The SignalR `GetLibraryPage` RPC already returns a wrapped object; it
simply needs `suggestions` added to the anonymous object.

**Alternatives considered**:
- Separate `/library/suggestions?q=X` endpoint — rejected because it
  requires a second network round-trip, which could push total latency over
  800 ms on slow connections (SC-004).
- Adding `suggestions` to the `X-Suggestions` header — rejected because
  encoding a structured list in a header is fragile (special chars, length
  limits) and non-standard.

---

## R5 — Minimum Query Length and Threshold Tuning

**Question**: What edit-distance threshold prevents excessive false positives
while satisfying FR-001 (≤ 2 character difference)?

**Decision**:

| Query length | Activation | Threshold |
|---|---|---|
| 1–2 characters | Substring only (FR-003) | N/A |
| 3–5 characters | OSA fuzzy | distance ≤ 1 |
| 6+ characters | OSA fuzzy | distance ≤ 2 |

**Rationale**: A threshold of 2 against a 3-character query would match
almost any 3-character string (half the library). Tighter threshold for
short queries avoids noise while still catching the single-transposition
case (e.g., "teh" → "the").

For suggestions the threshold is relaxed: `distance / max(len_q,
len_token) ≤ 0.5` (50% normalised similarity). This is intentionally
broader to surface candidates when the singer's input is very different
from anything in the library.

---

## R6 — No Database Migration Required

**Question**: Does this feature require schema changes?

**Decision**: **No migration needed.**

- `RelevanceTier` is a C# enum used only in the service layer; it is
  never persisted.
- `SearchSuggestion` is computed on-the-fly and not stored.
- The `Song` table schema is unchanged.
- Existing indexes `(SessionId, AddedAt)` and `(SessionId, Artist, Title)`
  already support the LIKE queries used in both phases.
