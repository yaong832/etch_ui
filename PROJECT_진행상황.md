# etch_ui / etchflask — 진행 상황 정리

> **갱신:** 2026-06-04  
> **저장소:** WPF [`yaong832/etch_ui`](https://github.com/yaong832/etch_ui) · Flask [`yaong832/farmui`](https://github.com/yaong832/farmui) (`C:\etchflask`)

---

## 1. 한 줄 요약

실장비 인터락·버튼·램프와 **가상 TM 이송(시뮬)** 이 WPF에서 동작하고, Flask는 텔레메트리·모듈·레시피·**sklearn ML AI**를 제공한다. WPF는 시뮬 1Hz JSONL로 **오프라인 재학습** 파이프라인까지 갖추었다.

---

## 2. 완료된 주요 기능

### Phase 2 — WPF UX·시뮬

| 항목 | 상태 |
|------|------|
| 공정 스텝·PhaseHint·FOUP ProgressBar | ✅ |
| PM(가상) / Load Lock(실접촉) 라벨 | ✅ |
| Stop 시 TM 홈·데모 가이드·**데모 진행** 시나리오 | ✅ |
| 레시피 XML (`Recipes/default.process.xml`) · PM 순서 | ✅ |

### Phase 3 — 이력·설정·Flask 연동

| 항목 | 상태 |
|------|------|
| 이벤트/알람 이력 DB·UI | ✅ |
| 설정 저장 + 관리자 비밀번호 재확인 | ✅ |
| Flask `modules/latest`, `recipe/active`, events | ✅ |
| 웹 **모듈 상태**·**레시피** 탭 | ✅ |
| HMI 테마·로그인·사용자 관리 | ✅ |

### AI — 규칙 스텁 → **실모델(sklearn)**

| 항목 | 상태 |
|------|------|
| Flask `etch_model.py` — joblib 로드·추론 | ✅ |
| `etch_ai.py` — ML 우선, 없으면 규칙 스텁 | ✅ |
| 데모·live 저장 시 `etch_ai_predict` 자동 갱신 | ✅ |
| WPF AI 패널 — **(ML)** / **(규칙 스텁)** 구분 | ✅ |
| 예상 알람·신뢰도 표시 (조언만) | ✅ |
| **시뮬 JSONL → 학습 → 배포** 파이프라인 | ✅ |

### 보안·계정

| 항목 | 상태 |
|------|------|
| 관리자 발급 계정 (공개 가입 없음) | ✅ |
| 비밀번호 변경·재설정·이벤트 로그 | ✅ |

---

## 3. AI 학습·모델 경로 (요약)

| 단계 | 위치 |
|------|------|
| WPF 수집 (1Hz) | `{exe}/data/ai_training_snapshots.jsonl` |
| 변환·학습 | `tools/ai/train_from_sim.ps1` |
| 학습 산출물 | `tools/ai/output/models/*.joblib` |
| Flask 추론 | `C:\etchflask/models/etch/` |

**상세:** [`docs/AI_학습_모델_경로.md`](docs/AI_학습_모델_경로.md) · [`tools/ai/README.md`](tools/ai/README.md) · [`C:\etchflask\ETCH_AI.md`](C:/etchflask/ETCH_AI.md)

```powershell
# 시뮬 5분+ 수집 후
cd d:\WPFProject\etch_ui
.\tools\ai\train_from_sim.ps1 -Deploy -Archive
# Flask 재시작
```

---

## 4. Git 커밋 이력 (최근)

### etch_ui

| 커밋 | 요약 |
|------|------|
| `6444974` | 데모 AI·예상 알람·활성 레시피·데모 진행 |
| `def0ed5` | 레시피 XML·PM 순서·Flask recipe |
| `2602091` | PROTO modules/recipe API |

### etchflask (farmui)

| 커밋 | 요약 |
|------|------|
| `4c00f9d` | 데모 AI 갱신·예상 알람·웹 AI |
| `4714723` | 웹 활성 레시피 탭 |
| `800ce19` | 텔레메트리·모듈·대시보드 (rebase 반영) |

---

## 5. 미완·보류

| 항목 | 비고 |
|------|------|
| `MainWindow.xaml.cs` 분리·빌드 경고 CS8600 | 리팩터링 |
| TwinCAT 현장 검증 | 장비 필요 |
| Storyboard 실도어·IEG3268 실 TM | 범위 밖/2단계 |
| Phase 4.7 자동 재학습 스케줄·API 권한 | 운영 정책 |
| `AlarmCatalog` A001~A006 문구 보강 | 문서/UX |
| 시뮬만 장기 수집 → 현장 정확도 검증 | ML 품질 |

체크리스트: [`docs/TODO.md`](docs/TODO.md)

---

## 6. 실행·데모

1. `C:\etchflask\run_flask.bat` (또는 `ETCH_USE_DB=1`)
2. `etch_ui` F5 → `admin` / `Admin1234`
3. **시뮬 허용** → **Start** 또는 **데모 진행**
4. 브라우저 `http://localhost:5000` — 모듈·레시피·AI 탭
5. `GET /api/etch/ai/status` — `engine: sklearn`, `ready: true` (모델 배포 후)

---

## 7. 문서 인덱스

| 문서 | 용도 |
|------|------|
| **PROJECT_진행상황.md** | 본 문서 (전체 진행) |
| `PROJECT_개요.md` | 아키텍처·현황 스냅샷 |
| `PROJECT_계획.md` | Phase·로드맵 |
| `docs/AI_학습_모델_경로.md` | 시뮬 학습·재학습 경로 |
| `docs/TODO.md` | 작업 목록 |
| `PROTO_실행순서.md` | 실행 순서 |
| `PROJECT_모듈상태_AI_계획.md` | 모듈·AI 설계 |
| `C:\etchflask\ETCH_AI.md` | Flask AI API |

---

*다음 갱신: Phase 1.2 현장 검증 또는 ML 재학습 워크플로 완료 시.*
