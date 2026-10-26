$ErrorActionPreference = 'Stop'
$script:RepoDir = Split-Path -Parent $PSScriptRoot
$script:ToolsDir = Join-Path $script:RepoDir '.tools'
$script:ProjectFile = Join-Path $script:RepoDir 'ChaseCamPlus\ChaseCamPlus.csproj'
[xml]$gameSettings = Get-Content -LiteralPath (Join-Path $script:RepoDir 'ChaseCamPlus\GameDir.targets') -Raw
$script:GameDir = [string]$gameSettings.Project.PropertyGroup.GameDir.InnerText
$localSettingsPath = Join-Path $script:RepoDir 'ChaseCamPlus\GameDir.local.targets'
if (Test-Path -LiteralPath $localSettingsPath) {
    [xml]$localSettings = Get-Content -LiteralPath $localSettingsPath -Raw
    $script:GameDir = [string]$localSettings.Project.PropertyGroup.GameDir
}
$env:DOTNET_CLI_HOME = Join-Path $script:ToolsDir 'dotnet-home'
$env:NUGET_PACKAGES = Join-Path $script:ToolsDir 'nuget-packages'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'
$dotnetCommand = Get-Command dotnet.exe -ErrorAction SilentlyContinue
if (-not $dotnetCommand -and (Test-Path -LiteralPath 'C:\Program Files\dotnet\dotnet.exe')) {
    $script:DotnetExe = 'C:\Program Files\dotnet\dotnet.exe'
} else {
    $script:DotnetExe = if ($dotnetCommand) { $dotnetCommand.Source } else { $null }
}
function Assert-ModSdk {
    if (-not $script:DotnetExe) { throw 'The .NET SDK is missing. Install the Windows x64 .NET 10 SDK.' }
    $sdkList = @(& $script:DotnetExe --list-sdks)
    if ($LASTEXITCODE -ne 0 -or $sdkList.Count -eq 0) {
        throw 'Only the runtime is installed. Install the Windows x64 .NET 10 SDK, then reopen your terminal.'
    }
}
function Assert-ModReferences {
    $required = @(
        'NuclearOption_Data\Managed\Assembly-CSharp.dll',
        'NuclearOption_Data\Managed\Mirage.dll',
        'NuclearOption_Data\Managed\Rewired_Core.dll',
        'NuclearOption_Data\Managed\UnityEngine.dll',
        'NuclearOption_Data\Managed\UnityEngine.CoreModule.dll',
        'NuclearOption_Data\Managed\UnityEngine.PhysicsModule.dll',
        'NuclearOption_Data\Managed\UnityEngine.AudioModule.dll',
        'NuclearOption_Data\Managed\UnityEngine.InputLegacyModule.dll',
        'NuclearOption_Data\Managed\UnityEngine.UIModule.dll',
        'NuclearOption_Data\Managed\UnityEngine.IMGUIModule.dll',
        'NuclearOption_Data\Managed\UnityEngine.UI.dll',
        'BepInEx\core\BepInEx.dll'
    )
    foreach ($relativePath in $required) {
        if (-not (Test-Path -LiteralPath (Join-Path $script:GameDir $relativePath))) {
            throw "Missing reference: $relativePath. Check ChaseCamPlus\GameDir.local.targets."
        }
    }
}