# Quickstart: Artist Exploration — Browse Mode

*How to run and manually verify this feature after implementation.*

---

## Prerequisites

- Chrome or Edge (File System Access API required)
- A karaoke library directory with CDG+MP3 files

---

## Running the app

```powershell
cd C:\Users\akros\Projects\Karamel-Web
dotnet run --project Karamel.Web
# Opens at http://localhost:5245
```

---

## Manual Verification Steps

### 1. Load a library and verify the artist list appears

1. Open http://localhost:5245
2. Click **Open Library Folder** and select a directory with karaoke files
3. Wait for the scan to complete
4. Click the **QR code** link (or navigate directly to `/singer?session={id}`)
5. The **Library** tab should be active by default
6. With no text in the search box: the artist list should render immediately
7. Each row shows: artist name (left) and song count (right, muted)
8. Artists are in alphabetical order (case-insensitive)

### 2. Tap an artist and verify song results appear

1. Tap any artist row in the list
2. The search input should populate with the artist name
3. Song results for that artist should load (same as manually typing the name)
4. The artist list should disappear, replaced by the song table

### 3. Clear the search and verify the artist list returns

1. Tap the ✕ button (or press Escape) to clear the search
2. The artist list should reappear immediately (no network request — already cached)
3. No loading spinner should appear on second visit

### 4. Verify multi-device behavior

1. Open the SingerView URL on a mobile device (via QR code)
2. The artist list should load on the mobile device (fetched from backend API)
3. Tap an artist → songs should appear

---

## API Smoke Test

```bash
# Replace {sessionId} with a real GUID from a running session
curl http://localhost:5245/api/sessions/{sessionId}/library/artists
# Expected: JSON array like [{"name":"ABBA","songCount":12},...]
```
