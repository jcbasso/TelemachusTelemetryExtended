param(
    [string]$KspRoot = "",
    [switch]$NoDeploy
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($KspRoot)) {
    $KspRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..\..")).Path
}

$source = Join-Path $KspRoot "PluginData\TelemachusTelemetryExtended\Source\TelemachusTelemetryExtended.cs"
$outputDir = Join-Path $KspRoot "GameData\TelemachusTelemetryExtended\Plugins"
$output = Join-Path $outputDir "TelemachusTelemetryExtended.dll"
$stagingDir = Join-Path $KspRoot "PluginData\TelemachusTelemetryExtended\Source\bin"
$stagingOutput = Join-Path $stagingDir "TelemachusTelemetryExtended.dll"

if (-not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir | Out-Null
}
if (-not (Test-Path $stagingDir)) {
    New-Item -ItemType Directory -Path $stagingDir | Out-Null
}

$sdkRoot = Join-Path ${env:ProgramFiles} "dotnet\sdk"
$roslynCandidates = Get-ChildItem -Path $sdkRoot -Filter "csc.dll" -Recurse -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -like "*\Roslyn\bincore\csc.dll" } |
    Sort-Object FullName -Descending

if ($roslynCandidates.Count -eq 0) {
    throw "Roslyn csc.dll not found under $sdkRoot. Install .NET SDK."
}

$roslynCsc = $roslynCandidates[0].FullName

$refs = @(
    (Join-Path $kspRoot "KSP_x64_Data\Managed\Assembly-CSharp.dll"),
    (Join-Path $kspRoot "KSP_x64_Data\Managed\UnityEngine.dll"),
    (Join-Path $kspRoot "KSP_x64_Data\Managed\UnityEngine.CoreModule.dll"),
    (Join-Path $kspRoot "KSP_x64_Data\Managed\mscorlib.dll"),
    (Join-Path $kspRoot "GameData\Telemachus\Plugins\Telemachus.dll")
)

foreach ($ref in $refs) {
    if (-not (Test-Path $ref)) {
        throw "Missing reference: $ref"
    }
}

$refArg = "/reference:" + ($refs -join ",")

& dotnet $roslynCsc `
    /nologo `
    /nostdlib+ `
    /target:library `
    /optimize+ `
    /langversion:latest `
    /out:$stagingOutput `
    $refArg `
    $source

if ($LASTEXITCODE -ne 0) {
    throw "Compiler failed with exit code $LASTEXITCODE"
}

if (-not (Test-Path $stagingOutput)) {
    throw "Build did not produce $stagingOutput"
}

if ($NoDeploy) {
    Write-Host "Built staging DLL: $stagingOutput"
    return
}

try {
    Copy-Item -Path $stagingOutput -Destination $output -Force
    Write-Host "Built and deployed: $output"
}
catch {
    Write-Warning "Built staging DLL, but deploy failed (likely locked by running KSP)."
    Write-Warning "Close KSP and copy this file manually to GameData\\TelemachusTelemetryExtended\\Plugins\\TelemachusTelemetryExtended.dll"
    Write-Host "Staging build: $stagingOutput"
}
