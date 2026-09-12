param(
    [string]$AdofaiDir = 'C:\Program Files (x86)\Steam\steamapps\common\A Dance of Fire and Ice',
    [string]$ModAssembly = (Join-Path $PSScriptRoot '..\bin\Debug\AdofaiRichPresence.dll')
)

$ErrorActionPreference = 'Stop'
$managed = Join-Path $AdofaiDir 'A Dance of Fire and Ice_Data\Managed'
foreach ($dependency in @('UnityEngine.CoreModule.dll', 'UnityModManager\UnityModManager.dll', 'UnityModManager\0Harmony.dll')) {
    [void][Reflection.Assembly]::LoadFrom((Join-Path $managed $dependency))
}
$assembly = [Reflection.Assembly]::LoadFrom((Resolve-Path -LiteralPath $ModAssembly).Path)
$instanceFlags = [Reflection.BindingFlags]'NonPublic,Instance'
$staticFlags = [Reflection.BindingFlags]'NonPublic,Static'
$recoveryType = $assembly.GetType('AdofaiRichPresence.Core.CallbackRecovery', $true)
$recovery = [Activator]::CreateInstance($recoveryType, $true)
$run = $recoveryType.GetMethod('Run', $instanceFlags)
$reset = $recoveryType.GetMethod('Reset', $instanceFlags)
$script:updates = 0
$script:cleanups = 0
$script:reports = 0
$script:failUpdate = $true
$script:failCleanup = $false
$script:failReport = $false
$update = [Action]{ $script:updates++; if ($script:failUpdate) { throw 'simulated update failure' } }
$cleanup = [Action]{ $script:cleanups++; if ($script:failCleanup) { throw 'simulated cleanup failure' } }
$report = [Action[Exception]]{ param($error) $script:reports++; if ($script:failReport) { throw 'simulated logging failure' } }

function Invoke-Frame([double]$seconds) {
    [void]$run.Invoke($recovery, @([TimeSpan]::FromSeconds($seconds), $update, $cleanup, $report))
}
function Assert-Counts([int]$u, [int]$c, [int]$r) {
    if ($script:updates -ne $u -or $script:cleanups -ne $c -or $script:reports -ne $r) {
        throw "Unexpected update/cleanup/report count: $script:updates/$script:cleanups/$script:reports"
    }
}

# Simulate 60 fps without sleeping. Repeated frames must not reconnect repeatedly.
for ($frame = 0; $frame -lt 1800; $frame++) { Invoke-Frame ($frame / 60.0) }
Assert-Counts 1 1 1
Invoke-Frame 30
Assert-Counts 2 2 2
Invoke-Frame 59.99
Assert-Counts 2 2 2
$script:failUpdate = $false
Invoke-Frame 60
Invoke-Frame 60.02
Assert-Counts 4 2 2

# Even simultaneous update, cleanup and logger failures cannot escape or spin.
$script:failUpdate = $script:failCleanup = $script:failReport = $true
Invoke-Frame 61
Assert-Counts 5 3 4
Invoke-Frame 61.02
Assert-Counts 5 3 4
[void]$reset.Invoke($recovery, @())
$script:failUpdate = $script:failCleanup = $script:failReport = $false
Invoke-Frame 62
Assert-Counts 6 3 4

# Call the real toggle and prefix methods, with no game/Discord startup.
$startup = $assembly.GetType('AdofaiRichPresence.Startup', $true)
$settings = [Activator]::CreateInstance($assembly.GetType('AdofaiRichPresence.Settings', $true))
$settings.EnableDiscord = $false
$settings.DebugLogging = $true
$startup.GetField('settings', $staticFlags).SetValue($null, $settings)
$toggle = $startup.GetMethod('OnToggle', $staticFlags)
$entryType = $toggle.GetParameters()[0].ParameterType
$entry = [Runtime.Serialization.FormatterServices]::GetUninitializedObject($entryType)
$loggerField = $entryType.GetField('Logger')
$logger = [Runtime.Serialization.FormatterServices]::GetUninitializedObject($loggerField.FieldType)
$loggerField.SetValue($entry, $logger)
$freeze = $assembly.GetType('AdofaiRichPresence.Core.RunFreezeState', $true)
$prefix = $assembly.GetType('AdofaiRichPresence.Core.MuteBuiltInPresencePatch', $true).GetMethod('Prefix', $staticFlags)
$freeze.GetField('IsFrozen', $staticFlags).SetValue($null, $true)
[void]$toggle.Invoke($null, @($entry, $true))
if (-not $prefix.Invoke($null, @())) { throw 'Built-in presence was muted while Discord was disabled' }
if ($freeze.GetField('IsFrozen', $staticFlags).GetValue($null)) { throw 'Old frozen state survived enable' }
if (-not [object]::ReferenceEquals($logger, $freeze.GetField('Logger', $staticFlags).GetValue($null))) {
    throw 'Logger was lost during enable'
}
if (-not $freeze.GetField('DebugLogging', $staticFlags).GetValue($null)) { throw 'Debug logging was not restored' }
$settings.EnableDiscord = $true
if ($prefix.Invoke($null, @())) { throw 'Changing EnableDiscord did not mute the built-in presence' }
$settings.EnableDiscord = $false
if (-not $prefix.Invoke($null, @())) { throw 'Changing EnableDiscord did not restore the built-in presence' }
[void]$toggle.Invoke($null, @($entry, $false))
$settings.EnableDiscord = $true
if (-not $prefix.Invoke($null, @())) { throw 'Disabled mod still mutes built-in presence' }
Write-Output 'PASS: 1800-frame retry suppression, timed recovery, cleanup/logger failures, reset, toggle and live settings transitions.'
