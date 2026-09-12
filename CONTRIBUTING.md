# Contributing

Thanks for helping improve ADOFAI Rich Presence. Keep changes focused, explain
the user-facing impact in the commit or pull request, and include a regression
test when a change affects the CDN server.

## Local checks

The mod build references assemblies from a local A Dance of Fire and Ice
installation. For a normal build that does not write into the game directory:

```powershell
dotnet build --no-restore
```

After a Debug build, run these Windows PowerShell checks against the compiled
mod without launching Unity or connecting to Discord:

```powershell
powershell -NoProfile -File scripts/Test-SettingsReset.ps1
powershell -NoProfile -File scripts/Test-LifecycleRecovery.ps1
```

Both scripts accept `-AdofaiDir` for a non-default game install. The lifecycle
check uses simulated time to verify retry behavior, cleanup failures and mod
toggle transitions; it does not replace an in-game test.

If the game is installed elsewhere, pass its path explicitly:

```powershell
dotnet build -c Game -p:AdofaiDir="D:\Games\A Dance of Fire and Ice"
```

Run the CDN checks from the merged repository root:

```powershell
node --check cdn-server/server.js
Push-Location cdn-server
npm test
Pop-Location
```

Do not commit `bin/`, `obj/`, `cdn-server/node_modules/`, CDN storage files, or
environment files. Never put Discord application secrets, CDN upload secrets,
or deployment API keys in source control.

## Pull requests

GitHub Actions runs the following checks on `main` pushes and every pull request:

- **Repository CI**: production `CallbackRecovery` source regression scenarios on
  Windows/Linux with .NET 8; release metadata consistency and PowerShell syntax.
- **CDN server CI**: Node tests, both Docker build contexts, container health and upload smoke tests.
- **CodeQL**: JavaScript/TypeScript and GitHub Actions security analysis, also weekly.

Run the portable checks locally with:

```powershell
dotnet run --project tests/CoreChecks/CoreChecks.csproj --configuration Release
pwsh -NoProfile -File scripts/Test-Repository.ps1
```

These hosted checks do **not** build the complete net48 Unity mod or run its UMM
integration checks: proprietary game assemblies stay on the developer's machine.
Continue running the full local build and both compiled-mod scripts above for C#
changes. Do not enable self-hosted runners for untrusted PRs or upload game DLLs.

Dependabot opens weekly reviewable PRs for Actions, NuGet and both Dockerfiles.
Dependency PRs are not automatically merged. NuGet updates need a full local mod
build and game compatibility verification before approval.

### Releases

Use a new version/tag for new code; never replace a published binary silently.
Update `Info.json` and `Repository.json` together, run the local build/tests and
record the exact commit and any unverified game/Discord behavior in release notes.
Package the three redistributable DLLs (`AdofaiRichPresence.dll`, `DiscordRPC.dll`,
`Newtonsoft.Json.dll`) and `Info.json` at the ZIP root. Optional documentation is
limited to LICENSE, README.md, CHANGELOG.md and AI_ATTRIBUTION.md. Do not include
Unity/game/UMM DLLs, Settings.xml, upload secrets or debug output.

**Release verification** runs when a release is published, or manually via
Actions → Release verification → Run workflow with an existing `vX.Y.Z` tag.
It downloads the ZIP, checks its GitHub SHA256 digest, exact file allowlist,
mod identity/version and PE headers, without executing or extracting it. It is a
post-publication integrity check, not a pre-publication gate or proof of gameplay.
Generated release notes group PRs by bug, enhancement, dependencies and documentation.

### Review checklist

- Describe the behavior that changed and the reason for it.
- Call out any settings, migration, deployment, or release metadata changes.
- Confirm the local checks above and mention anything that could not be tested,
  such as in-game Unity behavior.
- Keep generated binaries and release archives out of the source diff.
