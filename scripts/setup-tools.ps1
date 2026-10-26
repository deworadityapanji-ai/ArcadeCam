. (Join-Path $PSScriptRoot 'environment.ps1')
Assert-ModSdk
Assert-ModReferences
$logDirectory = Join-Path $script:RepoDir '.logs'
New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null
Start-Transcript -Path (Join-Path $logDirectory 'setup-tools.log') -Append | Out-Null
try {
    & (Join-Path $PSScriptRoot 'build.ps1')
    $toolPath = Join-Path $script:ToolsDir 'bin'
    if (-not (Test-Path -LiteralPath (Join-Path $toolPath 'ilspycmd.exe'))) {
        & $script:DotnetExe tool install ilspycmd --tool-path $toolPath --configfile (Join-Path $script:RepoDir 'NuGet.Config')
        if ($LASTEXITCODE -ne 0) { throw 'ILSpy installation failed. Rerun this script to continue.' }
    }
    $env:DOTNET_ROLL_FORWARD = 'LatestMajor'
    & (Join-Path $toolPath 'ilspycmd.exe') --version
    if ($LASTEXITCODE -ne 0) { throw 'ILSpy was installed but could not start.' }
    $codeCommand = Get-Command code.cmd -ErrorAction SilentlyContinue
    if ($codeCommand) {
        & $codeCommand.Source --install-extension ms-dotnettools.csharp
        if ($LASTEXITCODE -ne 0) { throw 'Install the Microsoft C# extension through VS Code Extensions to finish setup.' }
        $extensions = @(& $codeCommand.Source --list-extensions)
        if ($extensions -notcontains 'ms-dotnettools.csharp') { throw 'The Microsoft C# extension could not be verified.' }
    } else {
        throw 'VS Code command not found. Open VS Code Extensions and install the Microsoft C# extension.'
    }
    Write-Host 'Setup completed: baseline DLL built, ILSpy checked, C# extension installed.'
} finally {
    Stop-Transcript | Out-Null
}
