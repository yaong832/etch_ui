# WPF 시뮬 JSONL → CSV → sklearn 학습 → (선택) Flask 배포
param(
    [string]$Jsonl = "",
    [int]$MinRows = 80,
    [switch]$Deploy,
    [switch]$Archive
)

$ErrorActionPreference = "Stop"
$Repo = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
Set-Location $Repo

$exportArgs = @("tools/ai/export_training_dataset.py", "--min-rows", $MinRows)
if ($Jsonl) { $exportArgs += @("--input", $Jsonl) }

python @exportArgs
if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "수집 안내: WPF 실행 → 로그인 → 「시뮬 허용」→ 공정 Start → 5분 이상 RUNNING 유지"
    Write-Host "파일: bin\Debug\net8.0-windows\data\ai_training_snapshots.jsonl"
    exit $LASTEXITCODE
}

$trainArgs = @("tools/ai/train_sklearn.py", "--min-rows", $MinRows)
if ($Archive) { $trainArgs += "--archive" }
python @trainArgs
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

# CSV 스냅샷 보관 (재학습 비교용)
$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$dsArchive = Join-Path $Repo "tools\ai\output\datasets\archive"
New-Item -ItemType Directory -Force -Path $dsArchive | Out-Null
Copy-Item (Join-Path $Repo "tools\ai\output\training_dataset.csv") `
    (Join-Path $dsArchive "training_dataset_$stamp.csv") -Force

if ($Deploy) {
    $deployArgs = @("-File", (Join-Path $PSScriptRoot "deploy_model.ps1"))
    if ($Archive) { $deployArgs += "-ArchiveFlask" }
    & @deployArgs
}

Write-Host ""
Write-Host "완료. Flask 사용 시 서버 재시작 후 GET /api/etch/ai/status 확인."
