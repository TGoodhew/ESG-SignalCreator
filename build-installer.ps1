<#
.SYNOPSIS
  Build the ESG-SignalCreator Windows installer (WiX v5 MSI).

.DESCRIPTION
  Builds the solution in Release, then builds the WiX installer project against that output.
  The installer project is intentionally NOT part of ESG-SignalCreator.sln, so a dev machine
  without the WiX toolset can still build the app. WiX v5 is restored automatically from NuGet
  by `dotnet build` (no global tool install required). Runs headless for CI.

.PARAMETER Version
  Product version (a.b.c.d) stamped into the MSI / ARP entry. Defaults to 1.0.0.0.

.EXAMPLE
  ./build-installer.ps1 -Version 1.0.0.0
#>
param(
  [string]$Version = "1.0.0.0",
  [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot

# Locate MSBuild (VS 2022/2026). Override by setting $env:MSBUILD.
$msbuild = $env:MSBUILD
if (-not $msbuild) {
  $candidates = @(
    "C:\Program Files\Microsoft Visual Studio\18\Enterprise\MSBuild\Current\Bin\MSBuild.exe",
    "C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe",
    "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe"
  )
  $msbuild = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
}
if (-not $msbuild) { throw "MSBuild not found. Set `$env:MSBUILD to its full path." }

Write-Host "Building solution ($Configuration)..." -ForegroundColor Cyan
& $msbuild "$root\ESG-SignalCreator.sln" -t:Restore,Build -p:Configuration=$Configuration -v:minimal -nologo
if ($LASTEXITCODE -ne 0) { throw "Solution build failed." }

$harvest = Join-Path $root "ESG-SignalCreator.App\bin\$Configuration"
if (-not (Test-Path (Join-Path $harvest "ESG-SignalCreator.exe"))) {
  throw "App output not found at $harvest"
}

Write-Host "Building MSI (WiX v5) v$Version..." -ForegroundColor Cyan
& dotnet build "$root\ESG-SignalCreator.Installer\ESG-SignalCreator.Installer.wixproj" `
    -c $Configuration -p:ProductVersion=$Version -p:HarvestPath=$harvest
if ($LASTEXITCODE -ne 0) { throw "Installer build failed." }

$msi = Get-ChildItem -Path "$root\ESG-SignalCreator.Installer\bin" -Recurse -Filter *.msi |
       Sort-Object LastWriteTime -Descending | Select-Object -First 1
if ($msi) { Write-Host "Installer: $($msi.FullName)" -ForegroundColor Green }
else { throw "No .msi produced." }
