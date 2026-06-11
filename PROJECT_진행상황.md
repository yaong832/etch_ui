# etch_ui / etchflask — 진행 상황 정리

> **갱신:** 2026-06-04 (Codex Tier 1 HMI·검증 현황 반영)  
> **저장소:** WPF [`yaong832/etch_ui`](https://github.com/yaong832/etch_ui) · Flask **etchflask** (`C:\etchflask`)  
> **최근 커밋:** `0595a6e` — 정비 도구·Stop 재개·WARNING/ALARM·TM 보간·헤드리스 sim CLI

---

## 1. 한 줄 요약

실장비 인터락·버튼·램프와 **가상 TM 이송(시뮬)** 이 WPF에서 동작하고, Flask는 텔레메트리·모듈·레시피·**sklearn ML AI**를 제공한다. **발표용 데모·헤드리스 검증**은 Release CLI로 자동화되었고, **Codex Tier 1 HMI 설득력** 개선을 진행 중이다.

---

## 2. 진행도·완성도 (2026-06-04 평가)

| 지표 | 수치 | 비고 |
|------|------|------|
| 로드맵 진행도 | **~78%** | Phase 2~3 핵심 완료, Phase 4·현장 일부 |
| 제품 완성도 | **~62%** | HMI 설득력·Flask E2E·ML 배포·TwinCAT 미검 |
| 발표 준비도 | **~85%** | `--sim-*` PASS, 수동 B1~B8·Flask E2E 미실행 |

---

## 3. 완료된 주요 기능 (최근)

### 시뮬·운전 UX

| 항목 | 상태 |
|------|------|
| Stop → **PauseTransfer** (상태 유지) / Start → Resume 또는 새 LOT | ✅ `0595a6e` |
| WARNING에서 Tick 유지·전역 황색 모듈 | ✅ |
| ALARM 모듈 ALM 우선·`--sim-alarm` | ✅ |
| TM UI 200ms tick + 16ms 회전 보간 | ✅ |
| Load Lock 웨이퍼·진공 TM 끊김 수정 | ✅ |
| 정비 도구 (BM/Aligner/PM/Side/FOUP/1틱) | ✅ |
| 헤드리스 sim: `--sim-smoke`, `--sim-policy-batch`, `--sim-maintenance`, `--sim-alarm`, `--sim-dual-blade`, `--sim-efem-audit` | ✅ Release PASS |

### Phase 2~3 (기존)

| 항목 | 상태 |
|------|------|
| 공정 스텝·PhaseHint·FOUP ProgressBar | ✅ |
| PM(가상) / Load Lock(실접촉) | ✅ |
| 레시피 XML · Flask recipe/modules | ✅ |
| 이벤트/알람 DB·설정·HMI 테마 | ✅ |
| AI ML/sklearn + JSONL 재학습 파이프라인 | ✅ |

### Codex Tier 1 HMI (진행 중 → 이번 갱신)

| 항목 | 상태 |
|------|------|
| TM **hold reason** · 스케줄러 hint (`TmTransferSimulator.UiHints.cs`) | ✅ 코드 |
| 도식 **파이프라인·HOLD 배너·Dual blade** 텍스트 | ✅ UI 연동 |
| **ALARM/WARNING** 전폭 SafetyBanner + 인터락 패널 강조 | ✅ |
| PM1 Strip(amber) vs PM2~4 Etch(blue) 좌측 stripe | ✅ |
| **웨이퍼 타임라인** (장내 웨이퍼 위치·단계) | ✅ |
| **AI Top signals** (Flask `topSignals` + 센서 편차 fallback) | ✅ |
| `--sim-ui-hints` 헤드리스 검증 | ✅ |
| Flask Chart.js 로컬화 | ⏳ Tier 2 (Flask) |

---

## 4. 검증 현황

### 자동 (Release CLI)

| 명령 | 결과 |
|------|------|
| `--sim-smoke` | PASS |
| `--sim-policy-batch` | PASS |
| `--sim-maintenance` | PASS |
| `--sim-alarm` | PASS |
| `--sim-dual-blade` | PASS |
| `--sim-efem-audit` | PASS |
| `--sim-ui-hints` | PASS (신규) |

### 수동 (미실행)

| 항목 | 문서 |
|------|------|
| B1~B8 발표 체크리스트 | `발표용_데모_체크리스트.md` |
| Flask E2E (모듈·레시피·AI 탭) | `PROTO_실행순서.md` |
| `train_from_sim.ps1 -Deploy` 후 sklearn ready | `tools/ai/README.md` |
| TwinCAT 현장 | 장비 필요 |

---

## 5. Git 커밋 이력 (최근)

### etch_ui

| 커밋 | 요약 |
|------|------|
| `0595a6e` | 정비 도구·Stop 재개·WARNING/ALARM·TM 보간·sim CLI |
| `f1be170` / `5fb0194` | Load Lock·Flask 분리·TM 보간 등 |
| *(로컬 미커밋)* | Tier 1 HMI: UiHints·SafetyBanner·도식 패널 |

### etchflask

| 커밋 | 요약 |
|------|------|
| `4c00f9d` | 데모 AI·예상 알람·웹 AI |
| GitHub main `938f90e` | `docs/semitool-hmi-critical-feedback.md` (로컬 docs에 요약 추가 권장) |

---

## 6. Codex 피드백 요약

**출처:** [semitool-hmi-critical-feedback](https://github.com/yaong832/etch_ui/blob/main/docs/semitool-hmi-critical-feedback.md) (원격 main)

- 기능 프로토타입은 양호, **장비 HMI 설득력**이 약함
- Tier 1: hold reason, dual blade 가시성, PM Strip/Etch 차별, 알람 UI
- Tier 2~3: 웨이퍼 타임라인, Flask CDN/innerHTML, AI 근거, TM tick 비균일화

---

## 7. 미완·보류

| 항목 | 비고 |
|------|------|
| `MainWindow.xaml.cs` 분리·CS8600 | 리팩터링 |
| TwinCAT 현장 검증 | 장비 필요 |
| Storyboard 실도어·IEG3268 실 TM | 2단계 |
| Phase 4.7 자동 재학습 스케줄 | 운영 정책 |
| Flask ML joblib 배포 (`manifest`만 → 스텁 기본) | `-Deploy` 후 재시작 |
| Tier 2~3 HMI | 위 Codex 로드맵 |

체크리스트: [`docs/TODO.md`](docs/TODO.md) · [`발표용_데모_체크리스트.md`](발표용_데모_체크리스트.md)

---

## 8. 실행·데모

1. `C:\etchflask\run_flask.bat`
2. `etch_ui` F5 → `admin` / `Admin1234`
3. **시뮬 허용** → **Start** (Stop 후 **재개** 가능)
4. `--sim-smoke` 등 Release 검증: `dotnet run -c Release -- --sim-smoke`
5. 브라우저 `http://localhost:5000`

---

## 9. 문서 인덱스

| 문서 | 용도 |
|------|------|
| **PROJECT_진행상황.md** | 본 문서 |
| `PROJECT_계획.md` | Phase·로드맵 |
| `발표용_데모_체크리스트.md` | 수동 검증 |
| `PROTO_실행순서.md` | 실행·CLI |
| `docs/semitool-hmi-critical-feedback.md` | Codex HMI 피드백 (pull 또는 원격 참조) |
| `tools/ai/README.md` | ML 재학습 |

---

*다음 갱신: Tier 1 UI 커밋 후 또는 B1~B8 수동 검증 완료 시.*
