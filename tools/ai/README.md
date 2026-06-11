# AI 학습·배포 (식각 HMI)

## 헤드리스 JSONL 검증 (CI·로컬)

```bash
dotnet run -c Release -- --sim-ai-jsonl --ticks=120
```

시뮬 틱마다 `AiTrainingDataRecorder`와 동일 스키마로 JSONL을 쓰고, 필수 필드·`AlarmCatalog`↔AI 예측 행을 검증합니다.

## 시뮬만으로 기본 모델 만들기 (권장)

1. WPF: **시뮬 허용** → **Start** → **5분+** RUNNING  
2. PowerShell:

```powershell
cd d:\WPFProject\etch_ui
pip install scikit-learn joblib numpy
.\tools\ai\train_from_sim.ps1 -Deploy -Archive
```

3. Flask 재시작 → `GET /api/etch/ai/status` (`ready: true`)

**경로 상세:** [`docs/AI_학습_모델_경로.md`](../../docs/AI_학습_모델_경로.md)

## 합성 데이터 (시뮬 없이)

```powershell
python tools/ai/generate_synthetic_dataset.py -n 3000
python tools/ai/train_sklearn.py
.\tools\ai\deploy_model.ps1
```

## 스크립트

| 파일 | 역할 |
|------|------|
| `export_training_dataset.py` | `ai_training_snapshots.jsonl` → CSV |
| `train_sklearn.py` | RF 학습 → `output/models/` |
| `train_from_sim.ps1` | export + train (+선택 deploy) |
| `deploy_model.ps1` | → `C:\etchflask\models\etch` |
| `alarm_labels.py` | CSV `alarm_class` 도출 |

## WPF 수집 경로

- 런타임: `{exe}/data/ai_training_snapshots.jsonl`
- 디버그 예: `bin/Debug/net8.0-windows/data/ai_training_snapshots.jsonl`

## API (Flask)

- `POST /api/etch/sensor-data` — 저장 시 ML/스텁 자동 갱신
- `GET /api/etch/ai/latest`, `/api/etch/ai/status`
