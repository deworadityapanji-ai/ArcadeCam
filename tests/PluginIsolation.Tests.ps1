$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$scriptFile = Join-Path $repo 'scripts\isolate-plugins.ps1'
$activeFile = Join-Path $repo '.tools\plugin-isolation\active-session.txt'
$previousPointer = if (Test-Path -LiteralPath $activeFile) { Get-Content -LiteralPath $activeFile -Raw } else { $null }
if ($previousPointer) {
    $previousManifest = Join-Path $previousPointer.Trim() 'manifest.json'
    if ((Test-Path -LiteralPath $previousManifest) -and (Get-Content -LiteralPath $previousManifest -Raw | ConvertFrom-Json).State -ne 'Restored') {
        throw 'Restore the active isolation session before running this test.'
    }
}
$fixture = Join-Path $repo ('.tools\isolation-fixture-' + [guid]::NewGuid().ToString('N'))
$plugins = Join-Path $fixture 'BepInEx\plugins'
function Invoke-Isolation([string]$mode, [string]$sessionPath, [bool]$expectSuccess = $true) {
    $arguments = @('-NoProfile', '-ExecutionPolicy', 'RemoteSigned', '-File', $scriptFile, '-Action', $mode, '-GameDirectory', $fixture)
    if ($sessionPath) { $arguments += @('-SessionDirectory', $sessionPath) }
    # Expected failures are validated by exit code, without surfacing them as PowerShell errors.
    $savedPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    $output = & powershell.exe @arguments 2>&1
    $result = $LASTEXITCODE
    $ErrorActionPreference = $savedPreference
    if (($result -eq 0) -ne $expectSuccess) { throw "Unexpected $mode result: $output" }
}
try {
    foreach ($relative in @('ArcadeCam\ArcadeCam.dll', 'BepInEx.ConfigurationManager\BepInEx\plugins\ConfigurationManager\ConfigurationManager.dll',
                           'ExampleMod\Example.dll', 'ExampleMod\data\resource.bin', 'AnotherMod\Other.dll', 'LoosePlugin.dll')) {
        $file = Join-Path $plugins $relative
        New-Item -ItemType Directory -Path (Split-Path -Parent $file) -Force | Out-Null
        Set-Content -LiteralPath $file -Value $relative
    }
    foreach ($relative in @('BepInEx\disabledPlugins\previously-disabled.dll', 'BepInEx\config\example.cfg')) {
        $file = Join-Path $fixture $relative
        New-Item -ItemType Directory -Path (Split-Path -Parent $file) -Force | Out-Null
        Set-Content -LiteralPath $file -Value 'unchanged'
    }
    $before = @{}
    Get-ChildItem -LiteralPath $fixture -Recurse -File | ForEach-Object { $before[$_.FullName] = (Get-FileHash -LiteralPath $_.FullName).Hash }
    Invoke-Isolation 'Isolate' ''
    $session = (Get-Content -LiteralPath $activeFile -Raw).Trim()
    $manifestFile = Join-Path $session 'manifest.json'
    $manifestText = Get-Content -LiteralPath $manifestFile -Raw
    $manifest = $manifestText | ConvertFrom-Json
    if ($manifest.State -ne 'Isolated' -or $manifest.Entries.Count -ne 3 -or @(Get-ChildItem -LiteralPath $plugins).Count -ne 2) {
        throw 'Isolation moved/kept the wrong entries.'
    }
    if (-not (Test-Path -LiteralPath (Join-Path $fixture 'BepInEx\disabledPlugins\previously-disabled.dll')) -or
        -not (Test-Path -LiteralPath (Join-Path $fixture 'BepInEx\config\example.cfg'))) { throw 'Moved disabled plugins or config.' }
    # Reject manipulated manifest paths before moving anything.
    $manifest.Entries[0].Name = '..\escape'
    $manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $manifestFile -Encoding UTF8
    Invoke-Isolation 'Restore' $session $false
    Set-Content -LiteralPath $manifestFile -Value $manifestText -Encoding UTF8
    # Any collision aborts the whole restore before another entry moves.
    $collision = Join-Path $plugins 'LoosePlugin.dll'
    Set-Content -LiteralPath $collision -Value 'newly-installed-plugin'
    Invoke-Isolation 'Restore' $session $false
    if (-not (Test-Path -LiteralPath (Join-Path $session 'plugins\ExampleMod'))) { throw 'Collision preflight moved another entry.' }
    $keptCollision = Join-Path $fixture 'collision-kept.dll'
    foreach ($candidate in @($collision, $keptCollision)) {
        if (-not [IO.Path]::GetFullPath($candidate).StartsWith([IO.Path]::GetFullPath($fixture).TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) {
            throw 'Test move escaped the fixture.'
        }
    }
    [IO.File]::Move($collision, $keptCollision)
    Invoke-Isolation 'Restore' $session
    foreach ($file in $before.Keys) {
        if ((Get-FileHash -LiteralPath $file).Hash -ne $before[$file]) { throw "Restored bytes differ: $file" }
    }
    Invoke-Isolation 'Restore' $session
    Write-Output 'PASS: atomic isolation/restore; kept plugins; resource/DLL bytes; disabled-folder/config preservation; path traversal and collision refusal; repeat restore.'
} finally {
    if ($previousPointer) { Set-Content -LiteralPath $activeFile -Value $previousPointer -Encoding UTF8 }
    elseif (Test-Path -LiteralPath $activeFile) {
        $savedPointer = Join-Path $fixture 'completed-test-pointer.txt'
        $root = [IO.Path]::GetFullPath((Join-Path $repo '.tools')).TrimEnd('\') + '\'
        foreach ($candidate in @($activeFile, $savedPointer)) {
            if (-not [IO.Path]::GetFullPath($candidate).StartsWith($root, [StringComparison]::OrdinalIgnoreCase)) { throw 'Test pointer move outside tools.' }
        }
        [IO.File]::Move($activeFile, $savedPointer)
    }
}
