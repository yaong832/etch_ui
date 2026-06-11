# WPF HMI 없이 SimulatorSmokeTester 직접 실행 (etch_ui.dll 참조)
param(
    [switch]$Quick
)

$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$mainProj = Join-Path $root "etch_ui.csproj"
$runnerProj = Join-Path $PSScriptRoot "InternalSimRunner\InternalSimRunner.csproj"

Push-Location $root
try {
    dotnet build $mainProj -c Release | Out-Host
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    dotnet build $runnerProj -c Release | Out-Host
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    $runnerArgs = @()
    if ($Quick) { $runnerArgs += "--quick" }
    dotnet run -c Release --no-build --project $runnerProj -- @runnerArgs
    exit $LASTEXITCODE
}
finally {
    Pop-Location
}
