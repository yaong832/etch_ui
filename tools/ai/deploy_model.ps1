# sklearn 모델 → Flask models/etch 배포 (선택: 이전 Flask 모델 백업)
param(
    [string]$SourceDir = "$PSScriptRoot\output\models",
    [string]$FlaskDir = "C:\etchflask\models\etch",
    [switch]$ArchiveFlask
)

$ErrorActionPreference = "Stop"
$files = @("anomaly_classifier.joblib", "alarm_classifier.joblib", "manifest.json")
foreach ($f in $files) {
    $src = Join-Path $SourceDir $f
    if (-not (Test-Path $src)) {
        Write-Error "Missing: $src — train_from_sim.ps1 또는 train_sklearn.py 먼저 실행."
        exit 1
    }
}

if ($ArchiveFlask -and (Test-Path $FlaskDir)) {
    $stamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $archive = Join-Path $SourceDir "flask_deploy_archive\$stamp"
    New-Item -ItemType Directory -Force -Path $archive | Out-Null
    Copy-Item -Path (Join-Path $FlaskDir "*") -Destination $archive -Force -ErrorAction SilentlyContinue
    Write-Host "Flask backup: $archive"
}

New-Item -ItemType Directory -Force -Path $FlaskDir | Out-Null
foreach ($f in $files) {
    Copy-Item -Path (Join-Path $SourceDir $f) -Destination (Join-Path $FlaskDir $f) -Force
}
Write-Host "Deployed to $FlaskDir"
Get-ChildItem $FlaskDir -File
