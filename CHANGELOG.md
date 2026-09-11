# Changelog

## 1.1.0

- Auto-upload each workshop level's cover image to a CDN and use it as the Discord large image
- Add a "View on Workshop" button and a "Get this mod" button
- Show the level author in the status line
- Detect death and the pause menu via Harmony event hooks instead of polling, which could miss ADOFAI's near-instant auto-restart after a death
- Freeze the displayed elapsed time while paused/dead, and resend more often in that state to limit how far Discord's progress bar can drift between updates
- Fall back to `scrController`'s title for boss encounters, which don't populate `scnGame` during their intro
- Add per-mode small-image (badge) keys and a debug-logging toggle
- Reorganize the settings panel into tabs

## 1.0.0

- Initial release: level/artist/difficulty/BPM/progress/mode display, Discord Rich Presence via a dedicated Application, built-in presence muting.
