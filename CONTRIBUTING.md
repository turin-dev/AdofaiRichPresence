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

- Describe the behavior that changed and the reason for it.
- Call out any settings, migration, deployment, or release metadata changes.
- Confirm the local checks above and mention anything that could not be tested,
  such as in-game Unity behavior.
- Keep generated binaries and release archives out of the source diff.
