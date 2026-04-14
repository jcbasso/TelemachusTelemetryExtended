param(
    [string]$KspRoot = "",
    [switch]$NoDeploy
)

$ErrorActionPreference = "Stop"

$buildScript = Join-Path $PSScriptRoot "Source\build-telemetry-extended.ps1"
if (-not (Test-Path $buildScript)) {
    throw "Missing build script: $buildScript"
}

& $buildScript -KspRoot $KspRoot -NoDeploy:$NoDeploy
