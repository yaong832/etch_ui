# etchflask E2E — 서버 기동(선택) + WPF 헤드리스 HTTP 검증
param(
    [string]$FlaskUrl = "http://127.0.0.1:5000",
    [string]$FlaskDir = "C:\etchflask",
    [switch]$StartFlask,
    [switch]$RequireMl,
    [int]$SimTicks = 80
)

$ErrorActionPreference = "Stop"
$Repo = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
Set-Location $Repo

function Test-FlaskUp([string]$Url) {
    try {
        $r = Invoke-WebRequest -Uri "$Url/api/sensors" -UseBasicParsing -TimeoutSec 3
        return $r.StatusCode -eq 200
    } catch {
        return $false
    }
}

$flaskProc = $null
if (-not (Test-FlaskUp $FlaskUrl)) {
    if (-not $StartFlask) {
        Write-Host "Flask 미응답: $FlaskUrl"
        Write-Host "  C:\etchflask\run_flask.bat 실행 후 재시도하거나 -StartFlask 사용"
        exit 13
    }

    $bat = Join-Path $FlaskDir "run_flask.bat"
    if (-not (Test-Path $bat)) {
        Write-Error "run_flask.bat 없음: $bat"
    }

    Write-Host "Flask 시작: $bat"
    $flaskProc = Start-Process -FilePath $bat -WorkingDirectory $FlaskDir -PassThru -WindowStyle Minimized

    $deadline = (Get-Date).AddSeconds(25)
    while ((Get-Date) -lt $deadline) {
        if (Test-FlaskUp $FlaskUrl) { break }
        Start-Sleep -Milliseconds 500
    }

    if (-not (Test-FlaskUp $FlaskUrl)) {
        Write-Error "Flask 기동 타임아웃 ($FlaskUrl)"
    }
}

Write-Host "dotnet build -c Release ..."
dotnet build -c Release | Out-Null
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$args = @("-c", "Release", "--no-build", "--", "--sim-flask-e2e", "--flask-url=$FlaskUrl", "--ticks=$SimTicks")
if ($RequireMl) { $args += "--require-ml" }

Write-Host "dotnet run $($args -join ' ')"
dotnet run @args
$code = $LASTEXITCODE

if ($flaskProc -and -not $flaskProc.HasExited) {
    Stop-Process -Id $flaskProc.Id -Force -ErrorAction SilentlyContinue
}

if ($code -eq 0) {
    Write-Host ""
    Write-Host "Flask E2E PASS — 브라우저: $FlaskUrl"
    try {
        $ai = Invoke-RestMethod "$FlaskUrl/api/etch/ai/status"
        Write-Host "AI: ready=$($ai.ready) engine=$($ai.engine)"
    } catch { }
}

exit $code
