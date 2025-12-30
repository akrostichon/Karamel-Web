# REQUIREMENT 1
## Title: Extend PlayerView to support video files as songs

## Description:
The PlayerView component shall support playback of both MP3+CDG and video files (e.g., MP4). When a video file is loaded, it shall be parsed and handled as a song entity, consistent with how MP3+CDG files are processed.

## Acceptance Criteria:

PlayerView can successfully play MP3+CDG files.
PlayerView can successfully play video files (e.g., MP4, AVI).
Video files are parsed as Song objects with metadata (title, artist) - fallback to %artist - %title pattern is identical between mp3+cdg and videos.
Playback behavior (play, pause, stop, progress tracking) operates identically for both MP3+CDG and video formats.
Existing automated tests are updated or new ones created to validate video playback functionality.

## Notes/Implementation Considerations
Ensure minimal code duplication between MP3+CDG and video handling logic.
Update affected unit and integration tests accordingly.
Verify compatibility with existing media player libraries.

# REQUIREMENT 2
## Title: Enable Admins to Stop Playback After Current Song

## Description:
The Playlist View shall provide administrators with an option to stop playback automatically after the current song finishes.
When this option is active and the current song ends, the system shall behave as if no subsequent song is queued, and the NextSong View shall reflect this state accordingly.
An administrator may later issue a “Proceed Playback” command, which resumes normal playback, updating the NextSong View to display the next song in the queue.
The Playlist View displays an admin-only control labeled “Stop after current song” in the bottom. It shall result in a toast message and then shall be disabled.
The Playlist View also displays an admin-only control labeled "Proceed playback" that is disabled until the playback stops due to "Stop after current song" and the Playlist is non-empty.

## Acceptance Criteria
The Playlist View displays an admin-only control labeled “Stop after current song”.
When activated, playback automatically stops at the end of the current song.
The NextSong View shows no next song once playback stops.
When an admin issues the “Proceed Playback” command:
Playback resumes with the next song in the queue.
The NextSong View updates to show that song.
Normal playback flow remains unaffected when the stop option is not active.

## Notes/Implementation Considerations
Ensure this functionality is only available to admin users.
Maintain queue integrity — no songs should be removed or reordered when playback stops.
Update or extend automated tests to cover stop and resume scenarios.

# REQUIREMENT 3
## Title: Redesign NextSongView to Display Upcoming Songs in Playlist‑Style Layout

## Description:
The NextSongView shall be redesigned to embed a playlist‑style view focused on the upcoming song.
The upcoming song shall be displayed prominently (larger font, clear call to action).
Subsequent songs shall be displayed below in progressively smaller font sizes up to a configurable cutoff limit.
This view shall not support scrolling.
A QR code or link for adding new singers shall continue to be displayed in an unobtrusive position (e.g., lower left).

## Acceptance Criteria
The upcoming song is displayed in the center, in a larger font with a clear call‑to‑action element.
If the playlist is longer than 2, the upcoming song may be displayed in the center top.
The following songs are shown below in smaller fonts, styled similar to the Playlist View.
The number of displayed songs:
Minimum of 2 (if the playlist contains at least 2 songs).
Maximum of 10, determined dynamically based on available screen size.
The view is non‑scrollable.
When fewer songs remain in the queue than the display limit, only the existing songs are shown.
A link/QR code for adding new singers remains visible and functional.

## Notes/Implementation Considerations
Apply responsive design principles to determine how many songs are shown based on available viewport height.
Ensure readability and visual hierarchy between the current and next songs.
Verify consistent styling and layout alignment with the current Playlist View component.
Update automated UI tests to validate display limits and layout elements (no scroll, QR code visibility, font scaling).
