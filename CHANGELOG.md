# Changelog

## Unreleased

- Confirm full settings resets and provide an undo action that restores unsaved settings without persisting the backup

- Add an optional checkpoint usage counter to the Discord presence and clear-result details
- Refresh changed level-cover files automatically instead of reusing a stale CDN URL

## 1.2.0 - 2026-09-11

- Add a live example preview to the settings panel, including Discord's 128-character field limits
- Add Discord connection status and a manual reconnect action to the settings panel
- Add safe Discord toggle lifecycle, connection retry backoff, and CDN upload validation/cleanup
- Add clear-result details for Perfect, Early, and Late judgments
- Improve paused, dead, editor, and menu state handling so Discord status is less stale
- Add display presets and reorganize settings around core and detailed information
- Harden CDN uploads, rate limiting, image validation, storage failure handling, and container execution
- Merge the CDN server into the main repository with tests and CI

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
