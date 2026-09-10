# AdofaiRichPresence

A Dance of Fire and Ice mod that shows detailed, live game info on Discord (via a real
Discord Rich Presence Application, not the game's built-in one).

## Requirements

- Unity Mod Manager, already installed for this game (`Mods/` folder present).
- .NET SDK (for building only — not needed to just run the mod).

## Build

```
dotnet build -c Game
```

This compiles straight into
`A Dance of Fire and Ice\Mods\AdofaiRichPresence\`. Launch the game (Unity Mod
Manager loads it automatically) and it should show up in the UMM mod list, enabled
by default.

If your game is installed somewhere other than the default Steam path, pass
`-p:AdofaiDir="D:\Games\A Dance of Fire and Ice"` to the build command, or edit the
default in `AdofaiRichPresence.csproj`.

For a normal Debug build (output stays in `bin/Debug`, doesn't touch the game), just
run `dotnet build`.

## Setup (one-time, in-game)

1. Create a Discord Application at https://discord.com/developers/applications
   (just needs a name — no secrets required).
2. Copy its **Application ID**.
3. In-game, open the Unity Mod Manager window (default: Ctrl+F10), go to
   **ADOFAI Rich Presence**, and paste the ID into the settings panel.
4. (Optional) Upload images under **Rich Presence → Art Assets** on the Discord
   Application page, and enter their asset key names into the mod's image key
   fields, to show custom artwork instead of no image.

## What it shows

Configurable in the mod's settings panel:
- Level name & artist
- Progress % and remaining tiles
- Difficulty (1-10) and BPM
- Elapsed / total time
- Current mode (playing / paused / menu / editor)

The game's own built-in Discord presence is muted by default (toggle in settings)
to avoid the two fighting over the same Discord connection.

## Project layout

- `Startup.cs` — Unity Mod Manager entry point.
- `Settings.cs` — persisted settings + in-game settings UI.
- `Core/GameState.cs` — reads live values off `scrController`, `scnGame`,
  `scrConductor`, `scrLevelMaker`.
- `Core/PresenceManager.cs` — builds and pushes the Discord Rich Presence payload.
- `Core/Patches.cs` — Harmony patch that mutes the game's built-in presence.
