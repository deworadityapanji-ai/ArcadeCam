param(
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Debug',
    [switch]$RestoreOriginalChaseCamPlus
)
. (Join-Path $PSScriptRoot 'environment.ps1')
Assert-ModReferences
if (Get-Process -Name NuclearOption -ErrorAction SilentlyContinue) {
    throw 'Close Nuclear Option before installing the test build.'
}
$sourceDll = Join-Path $script:RepoDir "ChaseCamPlus\bin\$Configuration\net472\ArcadeCam.dll"
if (-not (Test-Path -LiteralPath $sourceDll)) { throw 'Build ArcadeCam before installing it.' }
$pluginDirectory = Join-Path $script:GameDir 'BepInEx\plugins\ArcadeCam'
$destinationDll = Join-Path $pluginDirectory 'ArcadeCam.dll'
$backupDirectory = Join-Path $script:ToolsDir ('backups\ArcadeCam-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
function Backup-File([string]$Path, [string]$Name) {
    if (Test-Path -LiteralPath $Path) {
        New-Item -ItemType Directory -Path $backupDirectory -Force | Out-Null
        Copy-Item -LiteralPath $Path -Destination (Join-Path $backupDirectory $Name)
        Write-Host "Backed up $Name outside the plugins folder: $backupDirectory"
    }
}
Backup-File $destinationDll 'ArcadeCam.dll'
$oldDll = Join-Path $script:GameDir 'BepInEx\plugins\ChaseCamPlus\ChaseCamPlus.dll'
$oldConfig = Join-Path $script:GameDir 'BepInEx\config\ChaseCamPlus.cfg'
$newConfig = Join-Path $script:GameDir 'BepInEx\config\ArcadeCam.cfg'
$isDevelopmentFork = $false
if (Test-Path -LiteralPath $oldDll) {
    $version = [Reflection.AssemblyName]::GetAssemblyName($oldDll).Version
    $isDevelopmentFork = $version -ge [version]'2.0.1.0' -and $version -le [version]'2.0.4.0'
}
# Only import settings from our known development versions, never from another user's original.
if ($isDevelopmentFork -and (Test-Path -LiteralPath $oldConfig) -and -not (Test-Path -LiteralPath $newConfig)) {
    $developmentConfig = [IO.File]::ReadAllText($oldConfig)
    $forceMatch = [regex]::Match($developmentConfig, '(?m)^Centering Force\s*=\s*([0-9.]+)')
    $hadCentering = $forceMatch.Success -and [float]::Parse($forceMatch.Groups[1].Value, [Globalization.CultureInfo]::InvariantCulture) -gt 0
    if ($developmentConfig -notmatch '(?m)^CenteringEnabled\s*=') {
        $switchValue = if ($hadCentering) { 'true' } else { 'false' }
        $developmentConfig = $developmentConfig.Replace('[Chase Free Look]', "[Chase Free Look]`r`nCenteringEnabled = $switchValue")
    }
    # Retain the previous off state, but give the new toggle a useful stored strength.
    if ($forceMatch.Success -and -not $hadCentering) {
        $developmentConfig = [regex]::Replace($developmentConfig, '(?m)^Centering Force\s*=\s*0(?:\.0*)?\s*$', 'Centering Force = 0.8')
    }
    [IO.File]::WriteAllText($newConfig, $developmentConfig)
    Write-Host 'Copied the development fork settings into the separate ArcadeCam config.'
}
if ($RestoreOriginalChaseCamPlus -and $isDevelopmentFork) {
    $backupRoot = Join-Path $script:ToolsDir 'backups'
    $original = Get-ChildItem -LiteralPath $backupRoot -Filter ChaseCamPlus.dll -Recurse |
        Where-Object { [Reflection.AssemblyName]::GetAssemblyName($_.FullName).Version -eq [version]'2.0.0.0' } |
        Sort-Object FullName | Select-Object -First 1
    if (-not $original) { throw 'No original 2.0.0 backup found. Omit -RestoreOriginalChaseCamPlus to keep the existing ChaseCamPlus install.' }
    Backup-File $oldDll 'ChaseCamPlus-development.dll'
    Backup-File $oldConfig 'ChaseCamPlus-development.cfg'
    Copy-Item -LiteralPath $original.FullName -Destination $oldDll -Force
    if ((Get-FileHash -LiteralPath $original.FullName).Hash -ne (Get-FileHash -LiteralPath $oldDll).Hash) { throw 'Original ChaseCamPlus restoration did not match its backup.' }
    if (Test-Path -LiteralPath $oldConfig) {
        # The original expects booleans for these keys. Preserve their visibility choices.
        $configText = [IO.File]::ReadAllText($oldConfig)
        $configText = [regex]::Replace($configText, '(?m)^(Waterline|LeftInstruments|RightInstruments|Compass)\s*=\s*(Aircraft|Camera|Hidden)\s*$', {
            param($match)
            $visible = if ($match.Groups[2].Value -eq 'Hidden') { 'false' } else { 'true' }
            return $match.Groups[1].Value + ' = ' + $visible
        })
        [IO.File]::WriteAllText($oldConfig, $configText)
    }
    Write-Host 'Restored the original ChaseCamPlus 2.0.0 backup alongside ArcadeCam.'
}
New-Item -ItemType Directory -Path $pluginDirectory -Force | Out-Null
Copy-Item -LiteralPath $sourceDll -Destination $destinationDll -Force
if ((Get-FileHash -LiteralPath $sourceDll).Hash -ne (Get-FileHash -LiteralPath $destinationDll).Hash) {
    throw 'Installed DLL does not match the build.'
}
Write-Host "Installed: $destinationDll"
Write-Host 'Launch the game and open Configuration Manager > ArcadeCam > ArcadeCam > Preset.'
Write-Host 'Bind the new ArcadeCam actions in Flight Controls. Old ChaseCamPlus bindings remain separate.'
Write-Host 'To use the original instead, turn EnableArcadeCam off and restart the game.'