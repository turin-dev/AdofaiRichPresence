# AdofaiRichPresence

[![Repository CI](https://github.com/turin-dev/AdofaiRichPresence/actions/workflows/repository.yml/badge.svg)](https://github.com/turin-dev/AdofaiRichPresence/actions/workflows/repository.yml)
[![CDN server CI](https://github.com/turin-dev/AdofaiRichPresence/actions/workflows/cdn-server.yml/badge.svg)](https://github.com/turin-dev/AdofaiRichPresence/actions/workflows/cdn-server.yml)
[![CodeQL](https://github.com/turin-dev/AdofaiRichPresence/actions/workflows/codeql.yml/badge.svg)](https://github.com/turin-dev/AdofaiRichPresence/actions/workflows/codeql.yml)

[Download the latest release](https://github.com/turin-dev/AdofaiRichPresence/releases/latest) ·
[Report a bug / request a feature](https://github.com/turin-dev/AdofaiRichPresence/issues/new/choose) ·
[Verification and contribution guide](CONTRIBUTING.md)

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

1. In-game, open the Unity Mod Manager window (default: Ctrl+F10) and select
   **ADOFAI Rich Presence**. The mod includes a default Discord Application ID,
   so the presence should work without creating an app first.
2. (Optional) To use your own Discord application, create one at
   https://discord.com/developers/applications, copy its **Application ID**, and
   paste it into the **Discord 연결** tab.
3. (Optional) Upload images under **Rich Presence → Art Assets** on the Discord
   Application page, then enter the exact asset key names in the **이미지** tab.
4. After changing settings, press Unity Mod Manager's **Save** button so the
   changes are persisted.

The **Discord 연결** tab shows the current connection state and includes a
**Discord 다시 연결** button for recovering after Discord starts or restarts.
Turning off **Discord 상태 표시 사용** stops this mod's connection and lets the
game's own Discord status run again.
The **표시 정보** tab includes an example preview so you can check the two
Discord text lines and their 128-character limits before starting a level.

**기본 설정 전체 복원** asks for confirmation before changing any settings,
including the CDN URL and upload secret. **복원 취소** restores the settings and
tab from immediately before the last reset, including unsaved edits. These
changes take effect immediately; press UMM's Save button to persist them. The
undo snapshot stays in memory only and is lost when the mod is reloaded.

To verify reset/undo against a Debug build without launching the game, run
`powershell -NoProfile -File scripts/Test-SettingsReset.ps1` after `dotnet build`.
Pass `-AdofaiDir` if the game uses a different install location.

## What it shows

Configurable in the mod's settings panel:
- Level name & artist
- Progress % and remaining tiles
- Optional checkpoint usage count
- Automatically refreshes the CDN cover URL when the source image changes
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

The optional upload service is included in this repository under `cdn-server/`.
Its own `README.md` documents deployment, upload authentication, proxy handling,
and the health endpoint. Run its tests from that directory with `npm test`.

For Dokploy or another Docker deployment that uses the repository root as its
build context, use the root `Dockerfile`. It starts the CDN service from
`cdn-server/` and listens on port `8787`. If the deployment context is set to
`cdn-server/` instead, use `cdn-server/Dockerfile`.
