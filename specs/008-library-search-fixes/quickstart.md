# Quickstart: Verifying the 3 Fixes

## Prerequisites

- Application running: `dotnet run --project Karamel.Web` → http://localhost:5245
- A session with a library of songs loaded (use the home page to select a directory)
- A library that contains songs by an artist whose name starts with a letter late in the alphabet (Q–Z) — e.g. artist "Queen", "The Who", "ZZ Top"

---

## UC1 — Artist-Name Search

1. Open the Singer page.
2. Type the artist name exactly (e.g. `Queen`) into the search box.
3. **Expected**: Results include songs WHERE THE ARTIST IS "Queen", even if "Queen" does not appear in the song title (e.g. "Bohemian Rhapsody", "Somebody to Love").
4. **Also expected**: Songs with "Queen" in the title (e.g. "Dancing Queen" by ABBA, "Killer Queen" by Queen) appear BEFORE the pure artist-matches (relevance ordering: title matches rank above artist-only matches).
5. **Regression check**: Searching for "Quueen" (typo) should return 0 results and show "Did you mean:" suggestions if the fuzzy service finds nearby terms.

---

## UC2 — Sticky Search Box

1. Open the Singer page with a populated library.
2. Type a common word (e.g. `love`) to get many results.
3. Scroll down through the result list until the top of the page is well above the viewport.
4. **Expected**: The search input is still visible at the top of the viewport — it did NOT scroll away.
5. **Also expected**: Typing in the still-visible search box while scrolled down updates the results and resets the scroll position.

---

## UC3 — Fixed Background Gradient

1. Open the Singer page.
2. Clear the search box to see artist browse mode (or type a common word to get 3000 results).
3. Note the gradient color at the top-left and bottom-right of the visible viewport.
4. Now type a very uncommon word to get only 2–3 results.
5. **Expected**: The gradient colors look identical to step 3 — the gradient cover the full viewport and looks the same regardless of how many results are shown.
6. **Also expected**: Scrolling through a long list does NOT cause the gradient to move or change — it stays pinned to the viewport like wallpaper.

---

## Running Automated Tests

```powershell
# Backend tests (covers UC1 — see LibraryApiTests.cs)
dotnet test Karamel.Backend.Tests -v minimal

# Frontend C# tests (no new tests added for UC2/UC3)
dotnet test Karamel.Web.Tests

# JavaScript tests (no changes)
cd Karamel.Web\wwwroot ; npm run test:run ; cd ..\..
```
