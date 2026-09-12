param(
    [string]$AdofaiDir = 'C:\Program Files (x86)\Steam\steamapps\common\A Dance of Fire and Ice',
    [string]$ModAssembly = (Join-Path $PSScriptRoot '..\bin\Debug\AdofaiRichPresence.dll')
)

$ErrorActionPreference = 'Stop'
$managed = Join-Path $AdofaiDir 'A Dance of Fire and Ice_Data\Managed'
[void][Reflection.Assembly]::LoadFrom((Join-Path $managed 'UnityEngine.CoreModule.dll'))
[void][Reflection.Assembly]::LoadFrom((Join-Path $managed 'UnityModManager\UnityModManager.dll'))
$assembly = [Reflection.Assembly]::LoadFrom((Resolve-Path -LiteralPath $ModAssembly).Path)
$settingsType = $assembly.GetType('AdofaiRichPresence.Settings', $true)
$fields = $settingsType.GetFields([Reflection.BindingFlags]'Public,Instance,DeclaredOnly')
$privateFlags = [Reflection.BindingFlags]'NonPublic,Instance'
$reset = $settingsType.GetMethod('ResetToDefaults', $privateFlags)
$undo = $settingsType.GetMethod('UndoReset', $privateFlags)
$tab = $settingsType.GetField('selectedTab', $privateFlags)
$backup = $settingsType.GetField('settingsBeforeReset', $privateFlags)
$confirmation = $settingsType.GetField('resetConfirmationPending', $privateFlags)
$settings = [Activator]::CreateInstance($settingsType)
$defaults = [Activator]::CreateInstance($settingsType)

function Assert-SettingValues($instance, $expected) {
    foreach ($field in $fields) {
        if (-not [object]::Equals($field.GetValue($instance), $expected[$field.Name])) {
            # Never print values: a setting can contain an upload secret.
            throw ('Setting was not restored: ' + $field.Name)
        }
    }
}

$original = @{}
$defaultValues = @{}
foreach ($field in $fields) {
    $defaultValues[$field.Name] = $field.GetValue($defaults)
    switch ($field.FieldType.FullName) {
        'System.Boolean' { $value = -not $field.GetValue($defaults) }
        'System.String' { $value = 'test-' + $field.Name }
        'System.Single' { $value = [single]7.5 }
        default { throw ('Add a test value for ' + $field.Name) }
    }
    $field.SetValue($settings, $value)
    $original[$field.Name] = $value
}
$tab.SetValue($settings, 4)
$confirmation.SetValue($settings, $true)
[void]$reset.Invoke($settings, @())
Assert-SettingValues $settings $defaultValues
if ($tab.GetValue($settings) -ne 0 -or $confirmation.GetValue($settings)) {
    throw 'Reset did not clear navigation/confirmation state'
}

# Persisted XML must contain current settings only, not the undo snapshot/secret.
$serializer = New-Object System.Xml.Serialization.XmlSerializer($settingsType)
$writer = New-Object System.IO.StringWriter
try {
    $serializer.Serialize($writer, $settings)
    $xml = $writer.ToString()
    if ($xml.Contains('settingsBeforeReset') -or $xml.Contains('test-CdnUploadSecret')) {
        throw 'Undo state leaked into persisted settings'
    }
} finally {
    $writer.Dispose()
}

# Edits after a reset must not mutate the saved undo snapshot.
$settings.CdnUploadUrl = 'https://changed.example/upload'
[void]$undo.Invoke($settings, @())
Assert-SettingValues $settings $original
if ($tab.GetValue($settings) -ne 4 -or $null -ne $backup.GetValue($settings)) {
    throw 'Undo did not restore navigation and discard the snapshot'
}
[void]$undo.Invoke($settings, @())
Assert-SettingValues $settings $original

# A second reset replaces the previous snapshot with the immediately prior state.
[void]$reset.Invoke($settings, @())
$settings.CdnUploadUrl = 'https://second.example/upload'
[void]$reset.Invoke($settings, @())
[void]$undo.Invoke($settings, @())
$secondExpected = $defaultValues.Clone()
$secondExpected.CdnUploadUrl = 'https://second.example/upload'
Assert-SettingValues $settings $secondExpected
Write-Output ('PASS: reset/undo, repeated reset, navigation, and XML privacy; ' + $fields.Count + ' settings checked.')
