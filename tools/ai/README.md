# AI 데이터 수집/학습 파이프라인 (로컬)

## 1) WPF 실행으로 데이터 수집

- 앱 실행 후 `data/ai_training_snapshots.jsonl`에 1초 주기로 스냅샷이 쌓입니다.
- 경로(디버그 기준): `bin/Debug/net8.0-windows/data/ai_training_snapshots.jsonl`

## 2) JSONL -> CSV 변환

```powershell
python tools/ai/export_training_dataset.py `
  --input bin/Debug/net8.0-windows/data/ai_training_snapshots.jsonl `
  --output tools/ai/output/training_dataset.csv
```

## 3) 베이스라인 학습/평가

```powershell
python tools/ai/train_baseline.py `
  --dataset tools/ai/output/training_dataset.csv `
  --model-out tools/ai/output/baseline_model.json
```

## 출력물

- `tools/ai/output/training_dataset.csv`: 학습 피처 데이터셋
- `tools/ai/output/baseline_model.json`: 규칙 임계치 + 평가 지표(accuracy/precision/recall)

## 참고

- 현재 베이스라인은 **규칙 기반 분류기**입니다.
- 다음 단계에서 Flask AI 엔드포인트에 붙일 때는 이 파일의 임계치를 로딩해 점수화를 시작하면 됩니다.
