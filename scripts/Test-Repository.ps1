$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$info = Get-Content (Join-Path $root 'Info.json') -Raw | ConvertFrom-Json
$repository = Get-Content (Join-Path $root 'Repository.json') -Raw | ConvertFrom-Json
if ($info.Version -notmatch '^\d+\.\d+\.\d+$') { throw 'Info.json must use a three-part release version.' }
$releases = @($repository.Releases | Where-Object { $_.Id -eq $info.Id -and $_.Version -eq $info.Version })
if ($releases.Count -ne 1) { throw 'Expected exactly one matching Repository.json release.' }
$expected = "https://github.com/turin-dev/AdofaiRichPresence/releases/download/v$($info.Version)/AdofaiRichPresence-$($info.Version).zip"
if ($releases[0].DownloadUrl -cne $expected) { throw 'Release download URL does not match the version.' }
foreach ($file in Get-ChildItem $PSScriptRoot -Filter '*.ps1') {
    $tokens = $null
    $parseErrors = $null
    [void][System.Management.Automation.Language.Parser]::ParseFile($file.FullName, [ref]$tokens, [ref]$parseErrors)
    if ($parseErrors.Count) { throw "$($file.Name): $($parseErrors -join '; ')" }
}
Write-Host 'PASS: release metadata and PowerShell syntax (not game integration).'
