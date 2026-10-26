# Building ArcadeCam

The plugin targets .NET Framework 4.7.2. The development scripts and managed regression tests use the .NET 10 SDK. You also need a local Nuclear Option installation with BepInEx 5; game assemblies are referenced locally and are not distributed.

## Configure your game directory

The default path is the usual Steam installation directory. To use another location, create `ChaseCamPlus/GameDir.local.targets`:

```xml
<Project>
  <PropertyGroup>
    <GameDir>C:\Path\To\Nuclear Option</GameDir>
  </PropertyGroup>
</Project>
```

This file is ignored by Git. Keep game DLLs, decompiled references and personal configurations out of source control.

## Build

From the repository root in PowerShell:

```powershell
powershell.exe -NoProfile -ExecutionPolicy RemoteSigned -File .\scripts\build.ps1
```

The script restores packages from the sources in `NuGet.Config` and builds the plugin. Output is `ChaseCamPlus/bin/Debug/net472/ArcadeCam.dll`. It does not install the DLL into the game.

For a Release build, add `-Configuration Release`. Do not distribute the game's DLLs or the test programs' copied dependencies.

## Install your build

Close Nuclear Option, then run:

```powershell
powershell.exe -NoProfile -ExecutionPolicy RemoteSigned -File .\scripts\install-mod.ps1
```

For a Release build, add `-Configuration Release`. The installer backs up the previous ArcadeCam DLL outside the plugins folder and verifies the installed file's hash. Ordinary updates retain settings. The optional historical ChaseCamPlus restoration switch is unnecessary for a normal install or update.

Use `scripts/check-environment.ps1` to inspect the local SDK and reference setup. Optional VS Code tasks are included. `scripts/setup-tools.ps1` additionally installs development tools; building the mod does not require those tools.

See [testing notes](TESTING.md) before distributing a changed build. Preserve the original [license](../LICENSE) and [third-party notices](../THIRD-PARTY-NOTICES.md).
