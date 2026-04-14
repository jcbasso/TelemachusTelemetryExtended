param(
    [string]$KspRoot = ""
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($KspRoot)) {
    $KspRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..\..")).Path
}

$projectRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$buildScript = Join-Path $PSScriptRoot "build-telemetry-extended.ps1"
$stagingDll = Join-Path $PSScriptRoot "bin\TelemachusTelemetryExtended.dll"
$releaseRoot = Join-Path $projectRoot "release\TelemachusTelemetryExtended"
$releasePluginDir = Join-Path $releaseRoot "GameData\TelemachusTelemetryExtended\Plugins"

& $buildScript -KspRoot $KspRoot -NoDeploy

if (-not (Test-Path $stagingDll)) {
    throw "Missing staging DLL after build: $stagingDll"
}

if (Test-Path $releaseRoot) {
    Remove-Item -Path $releaseRoot -Recurse -Force
}

New-Item -ItemType Directory -Path $releasePluginDir -Force | Out-Null
Copy-Item -Path $stagingDll -Destination (Join-Path $releasePluginDir "TelemachusTelemetryExtended.dll") -Force
Copy-Item -Path (Join-Path $projectRoot "README.md") -Destination (Join-Path $releaseRoot "README.md") -Force
Copy-Item -Path (Join-Path $projectRoot "openapi.yaml") -Destination (Join-Path $releaseRoot "openapi.yaml") -Force

Write-Host "Release folder ready: $releaseRoot"
