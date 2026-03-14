# Data Model: Library View Enhancements

**Feature**: 006-library-view-enhancements  
**Date**: 2026-03-10

---

## Overview

This feature introduces no new backend entities, no new database tables, and no new DTOs. The data model is unchanged. This document covers the **presentational data structures** (in-memory, component-scope only) used for the alphabet navigation UI.

---

## New Presentational Record: `ArtistGroup`

**Location**: `LibrarySearch.razor` `@code` section (private, component-scoped)

```csharp
/// <summary>
/// Groups a set of artists under a single letter for alphabet navigation display.
/// </summary>
private record ArtistGroup(char Letter, IReadOnlyList<ArtistItem> Artists);
```

| Field | Type | Description |
|-------|------|-------------|
| `Letter` | `char` | Uppercase letter (A–Z), or `'#'` for artists whose names begin with a non-letter character (e.g., "4 Non Blondes"). The `#` group is rendered in the artist list but has **no corresponding button** in the A–Z alphabet bar; users must scroll to reach it. |
| `Artists` | `IReadOnlyList<ArtistItem>` | Artists in this group, in their original sorted order |

**Lifecycle**: Recomputed from `LibraryState.Value.Artists` inside `BuildArtistGroups()`, called when `ArtistsLoaded` transitions to `true`. Stored in a private `_artistGroups` field. Not persisted; no Fluxor state.

---

## Derived State: `_activeLetters`

**Location**: `LibrarySearch.razor` `@code` section (private field)

```csharp
private HashSet<char> _activeLetters = [];
```

Set of letters that have at least one artist. Computed alongside `_artistGroups` by `BuildArtistGroups()`. Used in the alphabet bar template to conditionally apply `active` vs. `inactive` CSS class and `disabled` attribute.

---

## Existing Model: `ArtistItem` (unchanged)

**Location**: `Karamel.Web/Models/` (already exists from spec 005)

```csharp
public record ArtistItem(string Name, int SongCount);
```

No changes to `ArtistItem`. Source of truth for grouping logic.

---

## Data Flow

```
LibraryState.Artists (IReadOnlyList<ArtistItem>)
    ↓   BuildArtistGroups()
_artistGroups (IReadOnlyList<ArtistGroup>)   _activeLetters (HashSet<char>)
    ↓                                              ↓
Artist list markup (section headers + rows)   Alphabet bar markup (active/inactive buttons)
    ↓
scrollToLetter(char) → alphabetBridge.js → scrollIntoView
```

---

## State Transitions

| Condition | Artist Groups | Active Letters | Alphabet Bar Visible |
|-----------|-------------|----------------|----------------------|
| Library empty / not scanned | `[]` | `{}` | Hidden (FR-006) |
| Artists loading (`IsLoadingArtists = true`) | `[]` | `{}` | Hidden |
| Artists loaded, list empty | `[]` | `{}` | Hidden |
| Artists loaded, list non-empty | Populated | Populated | Visible |
| User types in search box (exits browse mode) | Irrelevant | Irrelevant | Hidden |

---

## No Backend Model Changes

- No new tables or EF migrations required.
- No new DTOs.
- No changes to `ArtistSummaryDto` (backend) or `ArtistDto` (frontend).
- No changes to `LibraryState`, `LibraryActions`, or `LibraryReducers`.
