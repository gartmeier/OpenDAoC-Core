<#
.SYNOPSIS
Builds the Detour library and the navmeshes for some regions, and installs both where the server looks for them.

.DESCRIPTION
Run it from anywhere, after installing the tools once:

    winget install Microsoft.VisualStudio.2022.BuildTools --override "--quiet --wait --add Microsoft.VisualStudio.Workload.VCTools --includeRecommended"
    winget install Kitware.CMake
    git clone https://github.com/OpenDAoC/OpenDAoC-BuildNav ..\OpenDAoC-BuildNav   (next to this repo, as BuildNav's README requires)

It builds Detour.dll from Pathing/Detour with CMake, builds OpenDAoC-BuildNav with dotnet, and runs it on the game client's zone data.
It then copies Detour.dll to lib/ and the navmeshes to navmesh/ in each server output folder (Debug/ and Release/) that exists, since the server loads them from its working folder.
The BuildNav commit that built the navmeshes goes in navmesh/BUILDNAV_COMMIT.txt. The output folders are gitignored, so none of it is checked in.

Building every zone needs about 5 GB of RAM to load; Hibernia's home region (200, the default) needs far less.

.EXAMPLE
pwsh Pathing/build-navmeshes.ps1
pwsh Pathing/build-navmeshes.ps1 -Regions 200,100 -Client "D:\Games\DAoC"
#>
param(
    # The game client folder, with its zones/ folder.
    [string] $Client = "C:\Program Files (x86)\OpenDAoC",
    [int[]] $Regions = @(200),
    [string] $BuildNav = (Join-Path $PSScriptRoot "..\..\OpenDAoC-BuildNav"),
    [string] $Dotnet = (Join-Path $env:USERPROFILE ".dotnet\dotnet.exe")
)

$ErrorActionPreference = "Stop"
$repo = Resolve-Path (Join-Path $PSScriptRoot "..")

function Require([bool] $ok, [string] $why) {
    if (-not $ok) { throw $why }
}

function Run([string] $exe, [string[]] $arguments) {
    & $exe @arguments
    Require ($LASTEXITCODE -eq 0) "$exe $($arguments -join ' ') failed with exit code $LASTEXITCODE."
}

Require (Test-Path (Join-Path $Client "zones")) "No zones/ folder in the game client folder $Client. Pass -Client."
Require ([bool] (Get-Command cmake -ErrorAction SilentlyContinue)) "CMake isn't on PATH. Install it with: winget install Kitware.CMake"
Require (Test-Path $Dotnet) "No dotnet at $Dotnet. Pass -Dotnet."
Require (Test-Path (Join-Path $BuildNav "OpenDAoC-BuildNav.sln")) "No OpenDAoC-BuildNav clone at $BuildNav. Clone it next to this repo: git clone https://github.com/OpenDAoC/OpenDAoC-BuildNav"

# Detour.dll, 64-bit, with 64-bit polygon references, as the navmeshes and Detour.Managed expect.
$detourSource = Join-Path $repo "Pathing\Detour"
$detourBuild = Join-Path $detourSource "Build"
Run cmake @("-S", $detourSource, "-B", $detourBuild, "-A", "x64", "-DRECASTNAVIGATION_DT_POLYREF64=ON")
Run cmake @("--build", $detourBuild, "--config", "Release")
$detour = Join-Path $detourBuild "Release\Detour.dll"
Require (Test-Path $detour) "CMake built no $detour."

# BuildNav, and the Detour.dll it uses for ladder links, in its working folder base/.
Run $Dotnet @("build", (Join-Path $BuildNav "OpenDAoC-BuildNav.sln"), "-c", "Release")
$buildNavExe = Get-ChildItem (Join-Path $BuildNav "bin\Release") -Recurse -Filter "OpenDAoC-BuildNav.exe" | Select-Object -First 1
Require ($null -ne $buildNavExe) "dotnet built no OpenDAoC-BuildNav.exe under $BuildNav\bin\Release."
$base = Join-Path $buildNavExe.DirectoryName "base"
New-Item -ItemType Directory -Force (Join-Path $base "lib") | Out-Null
Copy-Item $detour (Join-Path $base "lib\Detour.dll")

# The exe finds its .NET runtime through DOTNET_ROOT, which may not be where the system's dotnet is.
# It starts RecastDemo.exe from base/ by name, which Windows finds only on PATH.
$env:DOTNET_ROOT = Split-Path $Dotnet
$env:Path = "$base;$env:Path"

# BuildNav waits for a key once it's done, and without a console it fails there behind an error dialog instead.
# So it runs with its output in a log, and is closed once the log says it's done.
$log = Join-Path $base "buildnav.log"
$buildNavProcess = Start-Process $buildNavExe.FullName -ArgumentList "--daoc=`"$Client`"", "--regions=$($Regions -join ',')" -WorkingDirectory $base -NoNewWindow -PassThru -RedirectStandardOutput $log -RedirectStandardError "$log.err"

while (-not $buildNavProcess.HasExited -and -not (Select-String -Path $log -Pattern "All done." -SimpleMatch -Quiet)) {
    Start-Sleep -Seconds 5
}

if (-not $buildNavProcess.HasExited) { Stop-Process -Id $buildNavProcess.Id -Force }

Get-Content $log | Where-Object { $_ -match "\[Error\]|finished in|was empty" } | Write-Host

$navmeshes = Get-ChildItem (Join-Path $base "zones") -Filter "zone*.nav"
Require ($navmeshes.Count -gt 0) "BuildNav made no navmeshes in $base\zones."
$commit = (& git -C $BuildNav rev-parse HEAD).Trim()

$installed = 0

foreach ($output in @("Debug", "Release")) {
    $server = Join-Path $repo $output

    if (-not (Test-Path (Join-Path $server "CoreServer.dll"))) { continue }

    New-Item -ItemType Directory -Force (Join-Path $server "lib"), (Join-Path $server "navmesh") | Out-Null
    Copy-Item $detour (Join-Path $server "lib\Detour.dll")
    $navmeshes | Copy-Item -Destination (Join-Path $server "navmesh")
    Set-Content (Join-Path $server "navmesh\BUILDNAV_COMMIT.txt") "OpenDAoC-BuildNav $commit, regions $($Regions -join ',')"
    Write-Host "Installed Detour.dll and $($navmeshes.Count) navmeshes in $server."
    $installed++
}

Require ($installed -gt 0) "No server output folder (Debug/ or Release/ with CoreServer.dll) to install into. Build the server first."
Write-Host "Restart the server to load them. Its log says 'Loading NavMesh successful for zone ...' for each one."
