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
This can be realised as a toggle button with two states:
"Playback [stops|proceeds] after current song"
where the default option is "proceed".

Acceptance Criteria
The Playlist View displays an admin-only control labeled “Stop after current song”.
When activated, playback automatically stops at the end of the current song.
The NextSong View shows no next song once playback stops.
When an admin issues the “Proceed Playback” command:
Playback resumes with the next song in the queue.
The NextSong View updates to show that song.
Normal playback flow remains unaffected when the stop option is not active.
Notes/Implementation Considerations
Ensure this functionality is only available to admin users.
Maintain queue integrity—no songs should be removed or reordered when playback stops.
Update or extend automated tests to cover stop and resume scenarios.