param(
    [Parameter(Mandatory)][string]$ArchivePath,
    [Parameter(Mandatory)][ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version,
    [Parameter(Mandatory)][ValidatePattern('^[a-fA-F0-9]{64}$')][string]$Sha256
)
$ErrorActionPreference = 'Stop'
if ((Get-FileHash -LiteralPath $ArchivePath -Algorithm SHA256).Hash -ine $Sha256) {
    throw 'Release asset SHA256 differs from GitHub asset metadata.'
}
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $ArchivePath).Path)
try {
    # Deliberately do not extract or execute downloaded binaries.
    $required = @('AdofaiRichPresence.dll', 'DiscordRPC.dll', 'Newtonsoft.Json.dll', 'Info.json')
    $allowed = $required + @('LICENSE', 'README.md', 'CHANGELOG.md', 'AI_ATTRIBUTION.md')
    foreach ($entry in $archive.Entries) {
        if ($allowed -cnotcontains $entry.FullName) { throw "Unexpected archive entry: $($entry.FullName)" }
        if ($entry.Length -le 0 -or $entry.Length -gt 20MB) { throw "Invalid entry size: $($entry.FullName)" }
    }
    foreach ($name in $allowed) {
        $count = @($archive.Entries | Where-Object { $_.FullName -ceq $name }).Count
        if ($count -gt 1 -or ($required -ccontains $name -and $count -ne 1)) {
            throw "Missing or duplicate archive entry: $name"
        }
    }
    $reader = [IO.StreamReader]::new($archive.GetEntry('Info.json').Open())
    try { $info = $reader.ReadToEnd() | ConvertFrom-Json } finally { $reader.Dispose() }
    if ($info.Id -cne 'AdofaiRichPresence' -or $info.Version -cne $Version -or
        $info.AssemblyName -cne 'AdofaiRichPresence.dll' -or
        $info.EntryMethod -cne 'AdofaiRichPresence.Startup.Load') {
        throw 'Packaged mod identity, version or entry point is incorrect.'
    }
    foreach ($name in $required | Where-Object { $_.EndsWith('.dll') }) {
        $stream = $archive.GetEntry($name).Open()
        try {
            if ($stream.ReadByte() -ne 0x4D -or $stream.ReadByte() -ne 0x5A) {
                throw "$name is not a Windows PE binary."
            }
        } finally { $stream.Dispose() }
    }
} finally { $archive.Dispose() }
Write-Host "PASS: v$Version archive identity, contents, PE headers and SHA256. This does not test game compatibility."
