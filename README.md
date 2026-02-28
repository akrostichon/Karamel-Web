# Karamel-Web
Karamel-Web is a modern, cloud-enabled reimagining of the original Karamel karaoke player.
Built with Blazor WebAssembly, it brings karaoke management to the web while keeping your media files local.
Karamel-Web supports cdg+mp3 files either zipped or unzipped (one song and cdg per zip).

## Features:
- Singers can search your Song library and add their songs to the playlist
- Have your own Karaoke session and share it with whomever you like
- Playlist management for admin of session
- QR Code for easy session sharing
- Admin session controls: pause, resume, and advance the playlist from any device
- Runtime configuration: require singer names, allow/block reordering, set pause delay, switch theme
- Singer view includes a read-only up-next list toggle

## Getting Started

1. Open the app in Chrome or Edge (File System Access API required).
2. Choose your local song folder (CDG+MP3 or zipped pairs).
3. Share the session QR code or link with singers.

## Admin Controls

When you start a session the app generates two links:

| Link | Purpose |
|------|---------|
| **Singer link** | Read-only access — singers search the library and add songs |
| **Admin link** | Full control — includes the Session Controls panel on the Playlist page |

The admin link contains an `admin=1` query parameter and a signed `token` that is validated by the server. **Keep this link private** — anyone with it can control your session.

### Session Controls panel

The Session Controls panel appears on the **Playlist** page for admin tabs only. Switch to it using the segmented control in the top-right corner of the page.

| Button / Field | Action |
|---|---|
| ⏸️ **Pause** | Stops automatic playlist advancement after the current song ends. |
| ▶️ **Resume** | Re-enables automatic advancement; countdown restarts for the next song. |
| ⏭️ **Next** | Immediately advances to the next song (disabled while paused). |
| **Require singer name** | When checked, singers must enter their name before adding a song. |
| **Allow singers to reorder** | When unchecked, drag handles are hidden on the singer's playlist view. |
| **Pause between songs (seconds)** | After a song finishes the next song starts after this many seconds (0 = immediate, min 5 if non-zero, max 90). |
| **Theme** | Switch between Light and Dark theme; propagates to all connected tabs. |

Changes made in the config section take effect immediately for all connected tabs once you click **Save Settings**.

### Singer view — up-next list

Singers can tap the **Playlist** toggle button on their view to see a read-only list of upcoming songs.  
The list shows position, artist/title, and singer name but has no remove or reorder controls.

## Attributions

This project uses the following third-party libraries. See THIRD-PARTY-NOTICES.md for details and license information.

- CDGraphics.js — CDG rendering (MIT). https://github.com/bhj/cdgraphics
- jsmediatags — ID3 tag extraction (LGPL-3.0). https://github.com/aadsm/jsmediatags
- QRCode.js — QR code generation (MIT). https://github.com/davidshimjs/qrcodejs
- Fluxor — Flux pattern state management for Blazor (MIT). https://github.com/mrpmorris/Fluxor

For full license text, see the LICENSE file and THIRD-PARTY-NOTICES.md.
