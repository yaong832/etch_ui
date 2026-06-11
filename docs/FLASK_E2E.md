# Flask E2E 검증 (etchflask + WPF 클라이언트)

> **전제:** Flask 서버는 **별도 저장소** `etchflask` (`C:\etchflask` 로컬 예시).  
> WPF는 HTTP 클라이언트만 포함합니다.

---

## 빠른 실행 (오늘 마감용)

```powershell
# 1) Flask (별도 터미널 또는 -StartFlask)
C:\etchflask\run_flask.bat

# 2) 헤드리스 E2E (A1~A5 + 이벤트·이력)
cd D:\WPFProject\etch_ui
.\tools\flask\e2e_flask.ps1 -RequireMl

# 또는 Flask가 이미 떠 있을 때
dotnet run -c Release -- --sim-flask-e2e --require-ml --ticks=80
```

**성공 기준:** `sim_flask_e2e success=True`, `ai_ready=True`, `engine=sklearn`

---

## 자동 검증 항목 (`FlaskE2eTester`)

| # | API | 기대 |
|---|-----|------|
| A1 | `GET /api/sensors` | 200 · POST 후 `dataSource=demo`, `equipmentState=RUNNING` |
| A2 | `GET /api/etch/modules/latest?source=demo` | `success`, `count≥1` |
| A3 | `GET /api/etch/recipe/active?source=demo` | `success`, `recipe.id` |
| A4 | `GET /api/etch/ai/status` · `.../ai/latest` | `ready`(+ML 옵션), `anomaly_score` |
| — | `POST /api/etch/sensor-data` | WPF `HmiTelemetryPayloadFactory` 동일 페이로드 |
| — | `POST` + `GET /api/etch/events?source=demo` | 이벤트 영속 |
| — | `GET /api/etch/history?source=demo` | 데모 이력 |

---

## 수동 (발표 직전)

[`발표용_데모_체크리스트.md`](../발표용_데모_체크리스트.md) **A·B** 표:

1. 브라우저 `http://127.0.0.1:5000` — 실시간·모듈·레시피·**AI 진단** 탭
2. WPF F5 → 시뮬 허용 → Start → Flask 칩 **OK**
3. AI 패널 6초+ 후 문구 갱신

---

## ML 배포 확인

```powershell
cd D:\WPFProject\etch_ui
pip install scikit-learn joblib numpy
.\tools\ai\train_from_sim.ps1 -Deploy   # JSONL 있으면
# 또는 합성:
python tools/ai/generate_synthetic_dataset.py -n 3000
python tools/ai/train_sklearn.py
.\tools\ai\deploy_model.ps1
```

Flask **재시작** 후:

```powershell
Invoke-RestMethod http://127.0.0.1:5000/api/etch/ai/status
# ready: True, engine: sklearn
```

---

## CLI 옵션

| 인자 | 설명 |
|------|------|
| `--sim-flask-e2e` | E2E 실행 후 종료 (exit 0/13) |
| `--flask-url=http://127.0.0.1:5000` | Flask 베이스 URL |
| `--ticks=80` | E2E용 시뮬 워밍업 틱 |
| `--require-ml` | `ai/status`에서 `ready=true` & `engine=sklearn` 필수 |

---

## 문제 해결

| 증상 | 조치 |
|------|------|
| exit 13 · unreachable | `run_flask.bat` · 포트 5000 · FarmUI와 동시 실행 금지 |
| `recipe 없음` | POST sensor-data에 `recipe` 포함됨 — `--ticks` 늘리기 |
| `ai/latest` 없음 | POST 후 `stored=true`인지 확인 (`dataSource=demo`) |
| stub만 나옴 | `models/etch/*.joblib` 배포 + Flask 재시작 |

관련: [`docs/FLASK_FARMUI_분리.md`](FLASK_FARMUI_분리.md) · `C:\etchflask\ETCH_AI.md`
