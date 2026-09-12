param(
    [string]$AdofaiDir = 'C:\Program Files (x86)\Steam\steamapps\common\A Dance of Fire and Ice',
    [string]$ModAssembly = (Join-Path $PSScriptRoot '..\bin\Debug\AdofaiRichPresence.dll')
)
$ErrorActionPreference = 'Stop'
$managed = Join-Path $AdofaiDir 'A Dance of Fire and Ice_Data\Managed'
foreach ($dependency in @('UnityEngine.CoreModule.dll', 'UnityEngine.UnityWebRequestModule.dll', 'UnityModManager\UnityModManager.dll')) {
    [void][Reflection.Assembly]::LoadFrom((Join-Path $managed $dependency))
}
$binary = (Resolve-Path -LiteralPath $ModAssembly).Path
foreach ($dependency in @('DiscordRPC.dll', 'Newtonsoft.Json.dll')) {
    [void][Reflection.Assembly]::LoadFrom((Join-Path (Split-Path $binary -Parent) $dependency))
}
$assembly = [Reflection.Assembly]::LoadFrom($binary)
$settingsType = $assembly.GetType('AdofaiRichPresence.Settings', $true)
$settings = [Activator]::CreateInstance($settingsType)
$serializer = New-Object System.Xml.Serialization.XmlSerializer($settingsType)
$settings.Language = 'en'
$writer = New-Object IO.StringWriter
try {
    $serializer.Serialize($writer, $settings)
    $reader = New-Object IO.StringReader($writer.ToString())
    try { $loaded = $serializer.Deserialize($reader) } finally { $reader.Dispose() }
    if ($loaded.Language -ne 'en') { throw 'Language did not persist in XML' }
} finally { $writer.Dispose() }
$reader = New-Object IO.StringReader('<Settings><EnableDiscord>true</EnableDiscord></Settings>')
try { $legacy = $serializer.Deserialize($reader) } finally { $reader.Dispose() }
if ($legacy.Language -ne 'ko') { throw 'Legacy settings changed their default language' }

$flags = [Reflection.BindingFlags]'NonPublic,Instance'
$presenceType = $assembly.GetType('AdofaiRichPresence.Core.PresenceManager', $true)
$manager = $presenceType.GetConstructors($flags)[0].Invoke([object[]]@($null))
$snapshotType = $assembly.GetType('AdofaiRichPresence.Core.GameSnapshot', $true)
$modeType = $assembly.GetType('AdofaiRichPresence.Core.GameMode', $true)
$snapshot = [Activator]::CreateInstance($snapshotType)
$snapshot.LevelName = 'Example level'
$snapshot.Artist = 'Example artist'
$snapshot.Author = 'Example creator'
$snapshot.TotalTiles = 1000
$snapshot.RemainingTiles = 580
$snapshot.Accuracy = [single]0.9842
$snapshot.HasAccuracy = $true
$snapshot.HasCheckpointUsage = $true
$snapshot.CheckpointsUsed = 2
$snapshot.WorkshopId = '123456'
$settings.ShowMapCoverImage = $false
$settings.ShowModDownloadButton = $true
$settings.ShowCheckpointUsage = $true
$build = $presenceType.GetMethod('BuildPresence', $flags)
$expectedDetails = @{ MainMenu='Main menu'; LevelSelect='Selecting a level'; Playing='Example level'; Paused='Example level'; Dead='Example level'; Cleared='Cleared:'; Editor='Level editor:' }
foreach ($mode in [Enum]::GetNames($modeType)) {
    $snapshot.Mode = [Enum]::Parse($modeType, $mode)
    $presence = $build.Invoke($manager, @($snapshot, $settings))
    if (-not $presence.Details.StartsWith($expectedDetails[$mode])) { throw "Wrong English details for $mode" }
    $text = $presence.Details + ' ' + $presence.State
    if ($text -match '[\uAC00-\uD7A3]') { throw "Untranslated generated activity for $mode" }
    if ($presence.Details.Length -gt 128 -or $presence.State.Length -gt 128) { throw "Activity limit exceeded for $mode" }
    foreach ($button in $presence.Buttons) {
        if ($button.Label -notin @('View on Workshop', 'Get this mod')) { throw 'Untranslated activity button' }
    }
}
$snapshot.Mode = [Enum]::Parse($modeType, 'Playing')
$settings.ShowLevelAndArtist = $false
$settings.ShowAsListening = $true
$presence = $build.Invoke($manager, @($snapshot, $settings))
if (-not $presence.State.StartsWith('Listening')) { throw 'Listening state was not translated' }
$settings.ShowLevelAndArtist = $true
$snapshot.LevelName = [string][char]0xD55C
$presence = $build.Invoke($manager, @($snapshot, $settings))
if (-not $presence.Details.StartsWith($snapshot.LevelName)) { throw 'User-provided level title was changed' }

# No network connection: inject an invalid application ID and render the same error in both languages.
$presenceType.GetMethod('EnsureClient', $flags).Invoke($manager, @('invalid'))
$status = $presenceType.GetMethod('GetConnectionStatus', $flags)
$settings.Language = 'en'
$english = $status.Invoke($manager, @($settings))
if ($english -ne 'Error: Application ID must contain digits only.') { throw 'English connection error incorrect' }
$settings.Language = 'ko'
if ($status.Invoke($manager, @($settings)) -notmatch '[\uAC00-\uD7A3]') { throw 'Existing connection error did not switch to Korean' }
$settings.Language = 'en'
if ($status.Invoke($manager, @($settings)) -ne $english) { throw 'Existing connection error did not switch back to English' }
Write-Output 'PASS: language XML persistence, legacy defaults, seven activity modes, buttons, listening, unchanged level titles and live error translation. No game or Discord was launched.'
