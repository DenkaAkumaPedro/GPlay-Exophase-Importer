# Builds the plugin and (optionally) installs it into Playnite for development,
# or packs a .pext for distribution.
#
#   .\build.ps1            # build only (Release)
#   .\build.ps1 -Install   # build + copy into the Playnite extensions folder
#   .\build.ps1 -Pack      # build + produce dist\*.pext via Toolbox
#
# This machine runs Playnite from a custom location (D:\Progamas\Biblioteca\Playnite),
# so the default install target differs from %AppData%. Override with -ExtensionsDir.
param(
    [switch]$Install,
    [switch]$Pack,
    [string]$ExtensionsDir = "D:\Progamas\Biblioteca\Playnite\Extensions"
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$out = Join-Path $root "bin\Release"

dotnet build (Join-Path $root "GPlayExophaseImporter.csproj") -c Release
if ($LASTEXITCODE -ne 0) { throw "Build failed." }

if ($Install) {
    $dst = Join-Path $ExtensionsDir "GPlayExophaseImporter"
    New-Item -ItemType Directory -Force $dst | Out-Null
    Copy-Item (Join-Path $out "*") $dst -Recurse -Force
    Write-Host "Installed to $dst -- restart Playnite." -ForegroundColor Green
}

if ($Pack) {
    $possible = @(
        (Join-Path $env:LOCALAPPDATA "Playnite\Toolbox.exe"),
        "D:\Progamas\Biblioteca\Playnite\Toolbox.exe",
        (Get-Command Toolbox.exe -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source)
    )
    $toolbox = $possible | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
    if (-not $toolbox) { throw "Toolbox.exe not found. Tried: $($possible -join ', ')" }
    New-Item -ItemType Directory -Force (Join-Path $root "dist") | Out-Null
    & $toolbox pack $out (Join-Path $root "dist")
}