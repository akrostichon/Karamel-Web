# Karamel User Manual

Version: 1.0 · Last updated: March 2026

---

## Table of Contents

1. [Before You Begin — Browser Requirements](#1-before-you-begin--browser-requirements)
2. [Starting Your First Session](#2-starting-your-first-session)
3. [Sharing the Session](#3-sharing-the-session)
4. [Singer Flow — Adding Songs to the Queue](#4-singer-flow--adding-songs-to-the-queue)
5. [Managing the Playlist](#5-managing-the-playlist)
6. [Admin Controls Panel](#6-admin-controls-panel)
7. [Export Library](#7-export-library)
8. [Singer Up-Next Toggle](#8-singer-up-next-toggle)
9. [Tips & Troubleshooting](#9-tips--troubleshooting)

---

## 1. Before You Begin — Browser Requirements

> ⚠️ **Karamel requires Google Chrome or Microsoft Edge.**

Karamel uses the [File System Access API](https://developer.mozilla.org/en-US/docs/Web/API/File_System_API) to read your local song files directly in the browser — without uploading them anywhere. This API is currently only supported in **Chrome** and **Edge** (desktop). Firefox and Safari users will see an unsupported-browser message.

**Singers** (people who only browse and add songs) can use Karamel from **any modern mobile or desktop browser** — they do not need direct file access.

### Supported song formats

| Format | Requirements |
|---|---|
| **CDG + MP3 (bare)** | A `.cdg` file and a `.mp3` file with identical names in the same folder. |
| **CDG + MP3 (zipped)** | A `.zip` file containing exactly one `.cdg` and one `.mp3` pair. |
| **Video — `.mp4`** | A standard MPEG-4 video file. Artist and title are parsed from the filename. Maximum file size: 500 MB. |
| **Video — `.m4v`** | An iTunes-compatible MPEG-4 video file. Same scanning and size rules as `.mp4`. |

> **Note:** Video files cannot be inside a ZIP archive — only bare `.mp4` / `.m4v` files are scanned.

Subfolders are scanned automatically — organise your library any way you like.

---

## 2. Starting Your First Session

### Step 1 — Open the app

Navigate to the Karamel app in Chrome or Edge:  
👉 **[https://polite-grass-037bbc503.2.azurestaticapps.net/](https://polite-grass-037bbc503.2.azurestaticapps.net/)**

The **Home** page is the starting point for every session. All other pages (Playlist, Singer view, Player) become active once a session is running.

### Step 2 — Select your song folder

Click **Select Folder** and, in the system file picker, navigate to the root of your karaoke library. Select the folder (not an individual file) and click **Open / Select**.

Karamel will:

1. Scan the folder recursively for all supported song formats.
2. Extract artist and title metadata from ID3 tags (falling back to the filename if no tags are found).
3. Build the library and display a song count in the top bar.

> If your library is large (5 000+ songs) the scan may take 10–20 seconds.

### Step 3 — Your session is live

Once scanning completes, Karamel automatically creates a unique **session** identified by a UUID in the URL. Two links appear on the Home page:

| Link | Who uses it |
|---|---|
| **Singer link** | If you want to sing yourself or are in an **at home** session, gives access to search and allows to sing songs |
| **Admin link** | You (the host / KJ) — includes full session controls |

---

## 3. Sharing the Session

### QR Code

The **Singer link** is displayed as a QR code on the Home page and on the **Next Song** screen. Singers scan it with any phone camera — no app required.

> **Tip for venues:** Display the Next Song screen on a TV at the front of the room. It shows both the upcoming song and the QR code so singers can join mid-event without asking you for the link.
Copy, display, or share these links however works best for your venue (QR code, projector, printed card, etc.). Note that **session links are one-time links**. For security reasons we do not use fixed links. However, you could use a tinify URL tool or use a custom redirect.
Fixed links would allow destructive users to capture your session by adding a lot of songs even from their home.

### Admin link

The links shown on the session setup screen are admin links.
> ⚠️ **Security note:** The admin link contains a cryptographically signed token. Anyone who has this link can control your session. Do not share it on a public display or in a group chat.

### Multi-device behaviour

Karamel is designed so that **singers can join from any device on any network** — not just devices on the same Wi-Fi. The Singer view fetches the library from the backend, so no local file access is required on their device. Only the host tab (the one that selected the folder) needs to be Chrome/Edge on the host computer.

---

## 4. Singer Flow — Adding Songs to the Queue

When a singer opens the Singer link they land on the **Singer** page. From here they can:

### Search

Type any part of an artist name or song title into the search bar. Find songs even if your search term included typos.

### Browse by Artist

While you don't search for anything, see an alphabetical A–Z list of all artists. Tap an artist name to expand their song list, then tap a song to enqueue it.

### Adding a song

Tap **Add** (🎤 Add) next to any song.

> **Note:** Singers can add multiple songs. The current song order is set by the admin's drag-to-reorder feature (see [Managing the Playlist](#5-managing-the-playlist)).

---

## 5. Managing the Playlist

The host manages the queue from the **Playlist** page. The page has two views toggled by the segmented control in the top-right corner: **Playlist** and **Session Controls**.

### Playlist view

| Element | Description |
|---|---|
| **Now Playing** | The card at the top shows the currently playing song, artist, and singer name. |
| **Up Next** | The highlighted second card shows the song queued to play after the current one ends. |
| **Queue** | The remaining songs below, in order. |

### Reordering songs

Drag any song card by its **drag handle** (≡) to move it up or down in the queue. The new order takes effect immediately for all connected screens.

### Removing a song

Tap the **✕** button on a song card to remove it from the queue. The change is reflected on all connected devices in real time.

---

## 6. Admin Controls Panel

The **Session Controls** panel is available on the Playlist page for admin tabs only (opened via the admin link). Switch to it using the **Session Controls** tab in the top-right segmented control.

### Playback controls

| Button | What it does |
|---|---|
| ⏸️ **Pause** | Stops automatic advancement after the current song ends. The countdown timer will not start for the next song. |
| ▶️ **Resume** | Re-enables automatic advancement; the countdown restarts immediately. |
| ⏭️ **Next** | Immediately advances to the next song. Disabled while paused. |

### Session settings

| Field | Default | Description |
|---|---|---|
| **Require singer name** | Off | When enabled, singers must enter their name before adding a song. |
| **Allow singers to reorder** | On | Currently not implemented. |
| **Pause between songs (seconds)** | 10 | Delay before the next song starts after the current one ends. Set to `0` for immediate playback; otherwise minimum is `5`, maximum is `90`. |
| **Theme** | Light | Toggle between Light and Dark mode. The change propagates instantly to all connected tabs and devices. |

Click **Save Settings** to apply configuration changes. Settings take effect immediately for all connected users.

---

## 7. Export Library

The **Export** page (`/export`) lets you scan any local folder and download your song library as CSV files — entirely in your browser, with no data sent to any server.

👉 Open the export page: **[/export](https://polite-grass-037bbc503.2.azurestaticapps.net/export)**

> ⚠️ **Chrome or Edge required** — like the main session, the Export page uses the File System Access API. The folder you select here is independent of any active karaoke session.

### How to export

1. Navigate to the Export page.
2. Click **Select Folder** and choose your song library folder.
3. Wait for the scan to complete — a song count is shown when done.
4. Download the files you need.

### Export file reference

| File | Contents | Typical use |
|---|---|---|
| **artists.csv** | Two columns: `Artist`, `Title` — sorted A–Z by artist, then by title | Printed songbook sorted by artist name |
| **titles.csv** | Two columns: `Title`, `Artist` — sorted A–Z by title, then by artist | Printed songbook sorted by song title |
| **duplicates.csv** | Songs that share the same normalised artist + title combination | Library cleanup — find and remove duplicate Song files |

### What counts as a duplicate?

Karamel normalises each song's artist and title (lowercased, punctuation stripped) and groups any songs that resolve to the same normalised pair. Each duplicate group is listed together, so you can compare file names and decide which copy to keep.

---

## 8. Singer Up-Next Toggle

On the **Singer** page, singers can tap the **Playlist** toggle button to switch between the song search view and a read-only snapshot of the current queue.

The Up-Next list shows:

- Position in the queue
- Artist and song title
- Singer name (if provided)

Singers **cannot** reorder or remove songs from this view. It is purely informational, useful for planning when to add a second song or checking how long the wait will be.

---

## 9. Tips & Troubleshooting

### The folder scan is slow

Large libraries (5 000+ songs) can take 15–30 seconds on first scan. The browser is reading file metadata and ZIP contents entirely locally — there is no way to speed this up beyond having a fast local drive.

### Singers can't find a song

- Check that the file has both a `.cdg` and a `.mp3` with matching filenames.
- Check the format of the video file. `.avi` or `.mkv` are not supported.
- If the files are inside a ZIP, make sure the ZIP contains exactly one pair.
- Re-select the folder to force a fresh scan if you added files after opening the session.

### The QR code isn't working

Some QR code scanners (e.g. the one in Firefox) has problems with the complexity of the QR code.
Try using another QR scanner.

### The admin link stopped working

Admin tokens are tied to the session. If the session has expired (the server clears inactive sessions after 30 minutes of inactivity) you will need to start a new session from the Home page.

### The app says "Please open in Chrome or Edge"

The File System Access API is required for the host tab. Ask the person starting the session to use Chrome or Edge. Singers on other browsers can still join and add songs without any issues.

### Where is my data stored?

- **Song files:** Never leave your device. Only artist/title metadata is sent to the backend.
- **Session data:** Stored temporarily on the backend server and auto-deleted after 30 minutes of inactivity. Session data contains artist names, title names, duration, playlist items and session configuration.
- **Exported CSV files:** Generated entirely in your browser and downloaded directly. Nothing is uploaded.
