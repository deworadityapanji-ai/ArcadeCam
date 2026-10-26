. (Join-Path $PSScriptRoot 'environment.ps1')
$checks = @()
$sdkAvailable = $false
if ($script:DotnetExe) {
    $sdkList = @(& $script:DotnetExe --list-sdks)
    $sdkAvailable = $LASTEXITCODE -eq 0 -and $sdkList.Count -gt 0
}
$checks += [pscustomobject]@{ Component = '.NET SDK'; Ready = $sdkAvailable }
$checks += [pscustomobject]@{ Component = 'Git'; Ready = [bool](Get-Command git -ErrorAction SilentlyContinue) }
try { Assert-ModReferences; $referencesAvailable = $true } catch { $referencesAvailable = $false; Write-Warning $_.Exception.Message }
$checks += [pscustomobject]@{ Component = 'Game and BepInEx references'; Ready = $referencesAvailable }
$checks += [pscustomobject]@{ Component = 'ILSpy command line'; Ready = Test-Path -LiteralPath (Join-Path $script:ToolsDir 'bin\ilspycmd.exe') }
$checks | Format-Table -AutoSize
Write-Host "Game path: $script:GameDir"