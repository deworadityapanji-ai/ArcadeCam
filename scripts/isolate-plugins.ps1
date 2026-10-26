param(
    [ValidateSet('Inspect', 'Isolate', 'Restore')][string]$Action = 'Inspect',
    [string]$GameDirectory,
    [string]$SessionDirectory
)
. (Join-Path $PSScriptRoot 'environment.ps1')
if (-not $GameDirectory) { $GameDirectory = $script:GameDir }
$gamePath = [IO.Path]::GetFullPath($GameDirectory).TrimEnd('\')
$pluginsPath = Join-Path $gamePath 'BepInEx\plugins'
$storagePath = Join-Path $script:ToolsDir 'plugin-isolation'
$activeFile = Join-Path $storagePath 'active-session.txt'
$keep = @('ArcadeCam', 'BepInEx.ConfigurationManager')

function Assert-ChildPath([string]$Path, [string]$Root) {
    $resolvedPath = [IO.Path]::GetFullPath($Path)
    $resolvedRoot = [IO.Path]::GetFullPath($Root).TrimEnd('\') + '\'
    if (-not $resolvedPath.StartsWith($resolvedRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Path outside the intended directory: $resolvedPath"
    }
    return $resolvedPath
}
function Assert-RegularDirectory([string]$Path) {
    $item = Get-Item -LiteralPath $Path
    if (-not $item.PSIsContainer -or ($item.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
        throw "Expected an ordinary directory, not a link: $Path"
    }
}
function Write-Manifest {
    $manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $manifestFile -Encoding UTF8
}
function Get-EntryPaths($entry) {
    if ([IO.Path]::GetFileName([string]$entry.Name) -ne $entry.Name -or $entry.Name -in @('.', '..') -or $entry.Name -in $keep) {
        throw "Invalid isolation manifest entry: $($entry.Name)"
    }
    $original = Assert-ChildPath (Join-Path $pluginsPath $entry.Name) $pluginsPath
    $staged = Assert-ChildPath (Join-Path $payloadPath $entry.Name) $payloadPath
    return @($original, $staged)
}
function Assert-Hashes($entry, [string]$location) {
    foreach ($file in $entry.Dlls) {
        if ([string]::IsNullOrEmpty($file.RelativePath)) { $dll = $location }
        else { $dll = Assert-ChildPath (Join-Path $location $file.RelativePath) $location }
        if (-not (Test-Path -LiteralPath $dll -PathType Leaf) -or (Get-FileHash -LiteralPath $dll -Algorithm SHA256).Hash -ne $file.Sha256) {
            throw "Plugin DLL changed or is missing: $dll"
        }
    }
}

Assert-RegularDirectory $pluginsPath
# Only top-level enabled-plugin entries are candidates. BepInEx/disabledPlugins,
# config, patchers, core, and all other game directories are outside this scope.
if ($Action -eq 'Inspect') {
    Get-ChildItem -LiteralPath $pluginsPath -Force | ForEach-Object {
        [pscustomobject]@{ Action = $(if ($_.Name -in $keep) { 'KEEP' } else { 'MOVE' }); Name = $_.Name }
    }
    return
}
if (Get-Process -Name NuclearOption -ErrorAction SilentlyContinue) {
    throw 'Close Nuclear Option before isolating or restoring plugins.'
}
New-Item -ItemType Directory -Path $storagePath -Force | Out-Null
Assert-RegularDirectory $storagePath

if ($Action -eq 'Isolate') {
    if (Test-Path -LiteralPath $activeFile) {
        $previous = (Get-Content -LiteralPath $activeFile -Raw).Trim()
        $previousFile = Join-Path (Assert-ChildPath $previous $storagePath) 'manifest.json'
        if ((Test-Path -LiteralPath $previousFile) -and (Get-Content -LiteralPath $previousFile -Raw | ConvertFrom-Json).State -ne 'Restored') {
            throw 'An isolation session is already active. Restore it before starting another.'
        }
    }
    foreach ($folder in $keep) {
        Assert-RegularDirectory (Join-Path $pluginsPath $folder)
    }
    $entries = @(Get-ChildItem -LiteralPath $pluginsPath -Force | Where-Object Name -NotIn $keep)
    if ($entries.Count -eq 0) { throw 'There are no other enabled-plugin entries to isolate.' }
    foreach ($entry in $entries) {
        [void](Assert-ChildPath $entry.FullName $pluginsPath)
        if (($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) -or
            ($entry.PSIsContainer -and @(Get-ChildItem -LiteralPath $entry.FullName -Recurse -Force | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint }).Count -gt 0)) {
            throw "Linked plugin content needs manual inspection: $($entry.FullName)"
        }
    }
    $SessionDirectory = Assert-ChildPath (Join-Path $storagePath ('session-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))) $storagePath
    $payloadPath = Join-Path $SessionDirectory 'plugins'
    New-Item -ItemType Directory -Path $payloadPath | Out-Null
    Assert-RegularDirectory $SessionDirectory
    Assert-RegularDirectory $payloadPath
    $manifestFile = Join-Path $SessionDirectory 'manifest.json'
    $manifest = [pscustomobject]@{
        Format = 1; GameDirectory = $gamePath; State = 'Preparing'; Kept = $keep
        Entries = @($entries | ForEach-Object {
            $entryPath = $_.FullName
            $dlls = if ($_.PSIsContainer) { @(Get-ChildItem -LiteralPath $entryPath -Recurse -File -Filter '*.dll') }
                    elseif ($_.Extension -eq '.dll') { @($_) } else { @() }
            [pscustomobject]@{
                Name = $_.Name; Status = 'Pending'
                Dlls = @($dlls | ForEach-Object {
                    [pscustomobject]@{
                        RelativePath = $(if ($_.FullName -eq $entryPath) { '' } else { $_.FullName.Substring($entryPath.Length + 1) })
                        Sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
                    }
                })
            }
        })
    }
    Write-Manifest
    Set-Content -LiteralPath $activeFile -Value $SessionDirectory -Encoding UTF8
} else {
    if (-not $SessionDirectory) {
        if (-not (Test-Path -LiteralPath $activeFile)) { throw 'No isolation session has been recorded.' }
        $SessionDirectory = (Get-Content -LiteralPath $activeFile -Raw).Trim()
    }
    $SessionDirectory = Assert-ChildPath $SessionDirectory $storagePath
    Assert-RegularDirectory $SessionDirectory
    $payloadPath = Join-Path $SessionDirectory 'plugins'
    Assert-RegularDirectory $payloadPath
    $manifestFile = Join-Path $SessionDirectory 'manifest.json'
    $manifest = Get-Content -LiteralPath $manifestFile -Raw | ConvertFrom-Json
    if ($manifest.Format -ne 1 -or $manifest.GameDirectory -ne $gamePath) { throw 'Isolation manifest does not match this game directory.' }
    if ($manifest.State -eq 'Restored') { Write-Host 'This session is already restored.'; return }
}

# Validate every source/destination before the first recursive directory move.
# Destination collisions never merge or overwrite newly installed plugins.
foreach ($entry in $manifest.Entries) {
    $paths = Get-EntryPaths $entry
    if ([IO.Path]::GetPathRoot($paths[0]) -ne [IO.Path]::GetPathRoot($paths[1])) {
        throw 'Isolation requires the game and workspace on the same drive, so directory moves are atomic renames.'
    }
    $originalExists = Test-Path -LiteralPath $paths[0]
    $stagedExists = Test-Path -LiteralPath $paths[1]
    if ($originalExists -eq $stagedExists) { throw "Missing entry or destination collision: $($entry.Name)" }
    $existing = if ($originalExists) { $paths[0] } else { $paths[1] }
    $item = Get-Item -LiteralPath $existing
    if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Linked plugin entry: $existing" }
    Assert-Hashes $entry $existing
    if ($Action -eq 'Isolate' -and $stagedExists) { throw "Already staged: $($entry.Name)" }
}
$manifest.State = if ($Action -eq 'Isolate') { 'Isolating' } else { 'Restoring' }
Write-Manifest
try {
    foreach ($entry in $manifest.Entries) {
        $paths = Get-EntryPaths $entry
        if ($Action -eq 'Isolate') { $source = $paths[0]; $destination = $paths[1] }
        else { $source = $paths[1]; $destination = $paths[0] }
        if (Test-Path -LiteralPath $source) {
            # Paths above are verified immediate children of the explicit enabled
            # plugins directory and this workspace's separate isolation directory.
            # Same-drive rename: unlike recursive Move-Item fallback, a denied
            # directory rename cannot create a half-copied destination tree.
            if ((Get-Item -LiteralPath $source).PSIsContainer) {
                [IO.Directory]::Move($source, $destination)
            } else {
                [IO.File]::Move($source, $destination)
            }
        }
        $entry.Status = if ($Action -eq 'Isolate') { 'Isolated' } else { 'Restored' }
        Write-Manifest
    }
    $manifest.State = if ($Action -eq 'Isolate') { 'Isolated' } else { 'Restored' }
    Write-Manifest
} catch {
    $manifest.State = 'Interrupted'
    Write-Manifest
    throw "Plugin move stopped. The manifest records progress for a safe Restore retry. $($_.Exception.Message)"
}
Write-Host "$($manifest.State): $($manifest.Entries.Count) plugin entries."
Write-Host "Session: $SessionDirectory"
Write-Host 'ArcadeCam and Configuration Manager are kept enabled. Existing disabled plugins and configs were not moved.'
