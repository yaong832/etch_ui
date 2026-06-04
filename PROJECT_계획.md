# etch_ui · 식각 Load Lock HMI — 전체 계획서

> **목적:** 실장비(I/O 벤치) + 가상 반도체 라인(교육·발표) + Flask(원격·**AI**)를 **역할별로 분리**하고, 단계별로 무엇을 할지 고정한다.  
> **기준 경로:** WPF `D:\WPFProject\etch_ui` · Flask `C:\etchflask` · 가상 이송 참고 `D:\semitest\SemiconductorUi`  
> **현황 스냅샷:** [`PROJECT_개요.md`](PROJECT_개요.md)

---

## 1. 프로젝트 정의

### 1.1 한 줄

실제 센서·**Load Lock 접촉**·**버튼**·**램프**로 안전·공정을 다루고, TM·챔버·FOUP는 **`TmTransferSimulator` 가상 이송**으로 보여 주며, Flask로 원격 모니터링·**AI 진단(엔진)** 을 제공하는 식각 Load Lock 프로토타입 HMI이다.

### 1.2 성공 기준 (완료 정의)

| # | 기준 |
|---|------|
| S1 | EtherCAT 실측 시 압력·진동·온·습도·**Load Lock 접촉**이 인터락·화면·Flask에 반영된다 |
| S2 | **버튼** Start/Stop/Reset/Maint·**램프** DO·UI 제어가 권한·인터락과 함께 동작한다 |
| S3 | RUNNING 시 가상 TM 이송(FOUP→A→B→C→FOUP)이 도식·공정 스텝과 맞게 보인다 |
| S4 | 접촉은 **Load Lock 인터락만**; 챔버 도어는 **가상 이송**과만 연동된다 |
| S5 | 모니터링 PC에서 Flask로 실측·상태·이력을 **조회**할 수 있다 |
| S6 | AI **이상 점수·권고**가 Flask 추론 후 WPF·웹에 표시된다 (**인터락·Start 자동 변경 없음**) |

### 1.3 범위 밖 (명시적 제외)

- 실제 TM 서보(**IEG3268**), 챔버별 실도어 DI, FOUP 물리 슬롯
- TwinCAT 내부 식각 **레시피·시퀀스** (HMI는 감시·인터락·가상 시각화)
- MES / SECS-GEM
- **AI가 인터락·Start/Stop·램프 DO를 직접 제어**하는 것
- Flask 웹에서의 **공정 제어**
- WPF/.NET **내부에서 ML 모델 학습·추론**

---

## 2. 3계층 구조 (반드시 구분)

```
┌─────────────────────────────────────────────────────────────────┐
│  계층 A · 실장비 (EtherCAT / ADS)                                │
│  압력, 진동, 온습도 │ 접촉 DI5 (Load Lock) │ 버튼 DI0~3 │ 램프 DO0~3 │
└────────────────────────────┬────────────────────────────────────┘
                             │ 읽기 / 램프 쓰기
┌────────────────────────────▼────────────────────────────────────┐
│  계층 B · WPF (etch_ui)                                          │
│  인터락·상태·조작 │ TmTransferSimulator(가상) │ AI 조언 표시만    │
└────────────────────────────┬────────────────────────────────────┘
                             │ POST 텔레메트리 (~2초)
┌────────────────────────────▼────────────────────────────────────┐
│  계층 C · Flask (etchflask)                                      │
│  스냅샷·이력·이벤트 │ etch_ai 추론 │ 웹 대시보드·AI 탭           │
└─────────────────────────────────────────────────────────────────┘
```

### 2.1 계층 A — 실장비 (고정)

| 구분 | PLC | 방향 | HMI 역할 |
|------|-----|------|----------|
| 압력·진동·온·습도 | `NX_AD4203` | IN | 인터락, 표시, Flask·**AI 입력** |
| **접촉** | `NX_ID5342` **bit5** | IN | **Load Lock 닫힘**, A004, 도식 **Load Lock만** |
| **Start / Stop / Reset / Maint** | DI bit0~3 | IN | 공정·알람·유지보수 요청 |
| **Ready / Run / Warn / Alarm** | DO bit0~3 | OUT | `_state` → 램프 출력 |

**접촉 ≠ 챔버 도어 ≠ 버튼 ≠ 램프** (네 가지를 문서·UI에서 혼동하지 않음).

### 2.2 계층 B — WPF

| 모듈 | 책임 |
|------|------|
| `PlcAdsService` | 계층 A |
| `MainWindow` / `MainViewModel` | 1초 루프, 인터락, Flask POST |
| `TmTransferSimulator` | **가상 이송** (semitest `TransferController` 축소) |
| `EquipmentMotionBridge` | 접촉(실) + 시뮬(가상) → 도식 |
| `EtchFlaskClient` | sensor-data POST · (예정) `ai/latest` GET |
| `Security/*`, `AlarmCatalog` | 사용자·알람 A001~A006 |

### 2.3 계층 C — Flask

| 기능 | API/화면 | 제어 |
|------|----------|------|
| 실시간 KPI·차트 | `GET /api/sensors`, 실시간 탭 | 없음 |
| 이력·이벤트 | `GET /api/etch/history`, `events` | 없음 |
| **AI 엔진** | `GET /api/etch/ai/status`, `POST .../predict`, (예정) `GET .../ai/latest` | 없음 |
| AI UI | `etch_dashboard.html` **AI 진단** 탭 | 없음 |

---

## 3. 가상 이송 — `D:\semitest` 대응

| semitest | etch_ui | Phase |
|----------|---------|-------|
| `TransferController.TmPhase` | `TmTransferSimulator.SimPhase` | **0 ✓** |
| `Form1TmProcessor` 틱 | `MainWindow` 1초 `Tick()` | **0 ✓** |
| `TmVisualizationControl` | `EquipmentSchematicControl` + Animator | **0 ✓** |
| `ChamberController` | 가상 공정 타이머 | **2** |
| FOUP·Wafer 큐 | `_waferAt` / 큐 확장 | **5** |
| `IEG3268` | **제외** | — |

**루트:** `FOUP A → Chamber A → B → C → FOUP B` (반복)  
**가상 도어:** `WaitDoorPickupOpen` / `WaitDoorDropoffOpen` 시 해당 Region만 열림 표시.

---

## 4. 단계별 로드맵

### Phase 0 — 기반 ✅

- [x] WPF HMI, PLC ADS, 인터락, 알람, 로그인
- [x] Flask POST/GET, 웹 레이아웃(차트↑ 로그↓)
- [x] `TmTransferSimulator`, 접촉=Load Lock only
- [x] WPF 레이아웃 5:3:2.2 + 하단 로그
- [x] AI API 스텁 (`etch_ai.py`, 웹 AI 탭)

### Phase 1 — 실장비 신뢰도

| ID | 작업 | 상태 |
|----|------|:----:|
| 1.1 | EtherCAT·시뮬 허용 정책·로그 통일 | 🔜 |
| 1.2 | **접촉 열림** 시 RUNNING → ALARM + `TmTransferSimulator.Stop()` | ✅ |
| 1.3 | 버튼·램프·UI HW 매트릭스 체크리스트 (`PROTO_실행순서.md`) | ✅ 문서 |
| 1.4 | DO 램프 ↔ 화면 일치 현장 검증 | 🔜 현장 |

**완료:** S1, S2, S4.

### Phase 2 — 가상 이송·도식

| ID | 작업 |
|----|------|
| 2.1 | `ProcessStepLadder` ↔ `SimPhase` 라벨 |
| 2.2 | 도식 **(접촉)** Load Lock / **(가상)** 챔버 | ✅ |
| 2.3 | Stop 시 TM·웨이퍼 안전 위치 |
| 2.4 | (선택) semitest `ChamberController` 스타일 **가상 공정 타이머** |
| 2.5 | `AppSettings.TmPhaseTicks` |

**완료:** S3, §6 데모 3분.

### Phase 3 — 데이터·운영 (WPF·Flask 이력)

| ID | 작업 | 계층 |
|----|------|------|
| 3.1 | `event_logs` 조회 UI | B | ✅ |
| 3.2 | `alarm_history` + 화면 | B |
 | 3.3 | 레시피/인터락 임계치/유지보수 설정 UI (+변경 승인/이력) | B |
| 3.4 | 비밀번호 변경 | B |
| 3.5 | Flask 이력 영구 저장 | C |
| 3.6 | (선택) WPF 이벤트 → Flask events | B→C |

### Phase 4 — AI (Flask 엔진 + WPF·웹 표시)

> 상세: **§10**. 인터락·Start/Stop **비개입**.

| ID | 작업 | 위치 |
|----|------|------|
| 4.1 | `etch_ai.py` 추론 (`models/etch`, `ETCH_AI.md`) | C |
| 4.2 | `sensor-data` 수신 시 스코어 캐시 | C | ✅ |
| 4.3 | `GET /api/etch/ai/latest` | C | ✅ |
| 4.4 | WPF **AI 조언** (인터락 열): `AiScoreText`, `AiHintText` | B | ✅ |
| 4.5 | 웹 AI 탭: 카드 UI | C | ✅ |
| 4.6 | (선택) `[AI]` 하단 로그 (점수≥0.75) | B | ✅ |
 | 4.7 | AI 모델 운영: 재학습 잡/버전/평가 지표 관리 | C |
 | 4.8 | Flask API 권한: (읽기 전용 / 학습 승인) | C |

**완료:** S6.

### Phase 5 — semitest 심화 (선택)

- FOUP 25슬롯, `TransferController` 세부 Phase, Storyboard 도어

---

## 5. 화면 계획

### 5.1 WPF (확정 + 예정)

| 영역 | 비율 | 내용 |
|------|------|------|
| 좌 | 5* | 가상 도식, 상태·알람, **실측** 센서, 추세 |
| 중 | 3* | 인터락, 공정 스텝, **(Phase 4) AI 조언** |
| 우 | 2.2* | **램프**, **Start/Stop/Reset/Maint** |
| 하단 | ~140px | 운영 로그 |

### 5.2 Flask 웹

- 실시간: 실측 KPI, **가상 요약(라벨)**, 차트, 하단 로그 스트립
- **AI 진단 탭:** 모델 상태·점수·설명 (Phase 4.5)

### 5.3 문서

| 파일 | 역할 |
|------|------|
| **`PROJECT_계획.md`** | 본 계획서 |
| `PROJECT_개요.md` | 현황·구현 목록 |
| `WPF_장비UI_이식_계획.md` | semitest ↔ WPF 가상 이송 |
| `PROTO_실행순서.md` | 실행·데모·API |
| `PLC_IO_매핑.md` | 계층 A |
| `C:\etchflask\ETCH_AI.md` | AI 모델·API |

---

## 6. 발표·데모 (3분)

1. Flask + WPF 로그인, EtherCAT Connected, **실측** 센서  
2. Load Lock **접촉 닫힘** → 인터락 OK → **Start**  
3. **가상** TM FOUP→A→B→C, 챔버 **(가상) 도어** 열림  
4. (Phase 1.2) 접촉 열림 → ALARM, 이송 정지  
5. 모니터링 PC: Flask 실시간 + **(Phase 4)** AI 탭 점수  
6. **Reset** / **Stop** — AI·알람은 **조언만**, 자동 Start 없음  

---

## 7. 역할·권한

| 기능 | 작업자 | 관리자 |
|------|:------:|:------:|
| 모니터링·AI 조회 | ✓ | ✓ |
| Start / Stop | ✓ | ✓ |
| Alarm Reset / Maint | | ✓ |
| 사용자 관리 | | ✓ |
| 시뮬 허용 | ✓ | ✓ |

---

## 8. 리스크·가정

| 가정 | 리스크 | 대응 |
|------|--------|------|
| 접촉 bit5 = Load Lock 닫힘 | A004 오동작 | `PLC_IO_매핑.md` 검증 |
| AI ≠ 안전 PLC | 과신 | S6·UI에「조언」표기 |
| 가상 이송 ≠ 실 식각 | 오해 | 도식 (가상) 라벨 |
| Flask 이력 휘발 | 분석 불가 | Phase 3.5 |

---

## 9. 다음 액션 (권장 순서)

> **모듈별 상태 + AI 2트랙 상세:** [`PROJECT_모듈상태_AI_계획.md`](PROJECT_모듈상태_AI_계획.md)

| 순서 | Phase | 작업 |
|:----:|:-----:|------|
| 1 | M0 | 모듈 상태 집계 + Flask `modules[]` POST ✅ |
| 2 | M2 | Flask `modules/latest` + 웹 테이블 |
| 3 | M3 | AI-1 이상 + AI-2 알람 예측 |
| 4 | M4 | 참고 클러스터 UI 도식 (EFEM·PM4) |
| 5 | 3.3 | WPF 레시피·인터락 설정 |
| 6 | — | §6 데모 리허설 |

---

## 10. AI 기능 배치

### 10.1 원칙

| 규칙 | 이유 |
|------|------|
| **추론·학습 = Flask** | `etch_ai.py`, `models/etch`, Python/`ml_trainer` |
| **WPF = 조언 표시 + (승인 시) 레시피/임계치 반영** | AI는 권고만, 안전 조작은 현장(WPF) |
| **웹 = 분석·발표** | 기존 AI 탭, 이력·차트 병행 |
| **PLC·인터락·가상 TM = AI 미개입** | 안전은 규칙·실측·접촉 |

### 10.2 데이터

| AI 입력 O | AI 입력 X |
|-----------|-----------|
| 실측 압력·진동·온·습도 (`sensorsLive`) | TM/FOUP 위치, 가상 도어 |
| `equipmentState`, `alarmCode`, `interlockOk` | DI/DO raw |
| Flask 시계열 | `TmTransferSimulator` 내부 상태 |

### 10.3 API

| API | 상태 | 용도 |
|-----|------|------|
| `GET /api/etch/ai/status` | 있음 | 모델 유무 |
| `POST /api/etch/ai/predict` | 있음(스텁) | 스냅샷 추론 |
| `POST /api/etch/ai/train` | (예정) | 웹에서 학습 트리거(승인 필요) |
| `GET /api/etch/ai/models` | (예정) | 모델 버전·메타데이터 조회 |
| `GET /api/etch/ai/latest` | **예정** | WPF 2~3초 폴링 |

### 10.4 구현 순서

1. Flask: `predict` 고도화 + `sensor-data` 시 캐시  
2. `GET /api/etch/ai/latest`  
3. WPF: `MainViewModel` AI 속성 + 중앙 GroupBox  
4. 웹: AI 탭 카드 UI  
5. (선택) 로그만 — **Stop/인터락 자동 변경 금지**

---

## 11. 변경 이력

| 날짜 | 내용 |
|------|------|
| 2026-05-27 | 최초: 3계층, Phase 0~5, semitest, 실장비/가상 분리 |
| 2026-05-27 | AI §10·Phase 4 정리, S6, 접촉/버튼/램프 명확화, IEG3268 제외 |
| 2026-05-27 | `D:\semitest` 경로, Phase 4b 통합, 문서·로드맵 정합 |
