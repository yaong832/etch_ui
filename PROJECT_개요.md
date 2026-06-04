# etch_ui 프로젝트 개요 (현황 스냅샷)

> **단계·범위·AI 배치의 기준 문서:** [`PROJECT_계획.md`](PROJECT_계획.md)  
> **semitest 가상 이송:** [`WPF_장비UI_이식_계획.md`](WPF_장비UI_이식_계획.md)

---

## 1. 프로젝트가 하는 일

| 계층 | 역할 |
|------|------|
| **실장비 (A)** | 압력·진동·온습도, **Load Lock 접촉**, **버튼 4개**, **램프 4개** |
| **WPF (B)** | 인터락·조작·가상 도식(`TmTransferSimulator`)·**(예정) AI 조언 표시** |
| **Flask (C)** | 원격 조회·이력·**AI 추론 엔진** |

**하지 않는 것:** 실 TM(IEG3268), 챔버 실도어, AI의 Start/인터락 자동 제어, WPF 내 ML 학습.

---

## 2. 실장비 vs 가상

### 2.1 실장비 (EtherCAT)

| 장비 | PLC | HMI |
|------|-----|-----|
| 압력·진동·온·습도 | `NX_AD4203` | 인터락·표시·Flask·AI 입력 |
| **접촉** | DI **bit5** | **Load Lock 인터락 (A004)** — 챔버 도어 아님 |
| **버튼** | DI **0~3** | Start / Stop / Reset / Maintenance |
| **램프** | DO **0~3** | Ready / Run / Warn / Alarm **출력** |

### 2.2 가상 (소프트웨어)

- `Services/Simulation/TmTransferSimulator.cs` — FOUP A→A→B→C→FOUP B  
- 참고: `D:\semitest\SemiconductorUi` (`TransferController`, `Form1TmProcessor`)  
- 챔버 도어 열림 = 이송 Phase 전용 (`EquipmentMotionBridge`)

---

## 3. 시스템 구성

```
TwinCAT ←ADS→ WPF ──POST──► Flask ──► etch_ai (models/etch)
                  │              │
                  │              └── GET ← 모니터링 PC (실시간·AI 탭)
                  └── TmTransferSimulator (가상, PLC 무관)
```

| 구성 | 경로 |
|------|------|
| WPF | `D:\WPFProject\etch_ui` |
| Flask | `C:\etchflask` |
| semitest 참고 | `D:\semitest\SemiconductorUi` |

---

## 4. 코드 구조 (요약)

```
etch_ui/
├── MainWindow.xaml(.cs)     # 1초 루프, 인터락, Flask, 이송 Tick
├── Plc/PlcAdsService.cs
├── Services/
│   ├── EtchFlaskClient.cs
│   ├── EquipmentMotionBridge.cs
│   └── Simulation/TmTransferSimulator.cs
├── Equipment/               # 가상 도식
├── Security/                # SQLite
└── 문서/
    ├── PROJECT_계획.md
    ├── PROJECT_개요.md
    ├── PROTO_실행순서.md
    ├── PLC_IO_매핑.md
    └── WPF_장비UI_이식_계획.md
```

---

## 5. WPF 화면

| 영역 | 비율 | 내용 |
|------|------|------|
| 좌 5* | | 가상 도식, 알람, **실측** 센서 |
| 중 3* | | 인터락, 공정 스텝, **(예정) AI 조언** |
| 우 2.2* | | 램프, **버튼 4개** |
| 하단 | ~140px | 로그 |

---

## 6. 구현 현황

| 영역 | 상태 |
|------|------|
| PLC·인터락·알람·버튼·램프 | ✅ |
| `TmTransferSimulator` + 도식 | ✅ |
| Flask 텔레메트리·웹 | ✅ (AI 탭 스텁) |
| 접촉 열림 시 이송 정지 | 🔜 Phase 1.2 |
| DB 이벤트/알람 UI | 🔜 Phase 3 |
| AI `latest` + WPF 조언 패널 | 🔜 Phase 4 |
| IEG3268·실 TM | ❌ 범위 밖 |

### 6.1 Flask·AI (현재)

- `POST /api/etch/sensor-data`, `GET /api/sensors`
- `GET /api/etch/ai/status`, `POST /api/etch/ai/predict` (스텁)
- 웹 `etch_dashboard.html` — AI 진단 탭
- 상세: `C:\etchflask\ETCH_AI.md`

---

## 7. 로드맵 요약 (`PROJECT_계획.md`와 동일)

| Phase | 내용 |
|:-----:|------|
| **0** | 기반 ✅ |
| **1** | 실장비 신뢰도 (접촉→이송 정지 등) |
| **2** | 가상 도식·(접촉)/(가상) 라벨 |
| **3** | DB·설정 UI·Flask 이력 영구화 |
| **4** | **AI:** Flask 엔진 + WPF·웹 표시 |
| **5** | semitest 심화 (선택) |

---

## 8. 설정·실행

- `appsettings.json` — Flask URL, ADS 851, Interlock, `SimulationEnabled`
- 실행: `C:\etchflask\run_flask.bat` → `etch_ui` F5 → `admin` / `Admin1234`
- 상세: `PROTO_실행순서.md`

---

## 9. 발표용 한 줄

「실장비는 센서·Load Lock 접촉·버튼·램프로 안전을 지키고, 반도체 라인은 semitest 방식의 가상 TM 이송으로 보여 주며, AI 진단은 Flask에서 돌리고 WPF·웹에는 조언만 표시한다.」

---

## 10. 문서 인덱스

| 문서 | 용도 |
|------|------|
| **`PROJECT_계획.md`** | 전체 계획·Phase·AI §10 |
| **PROJECT_개요.md** | 본 문서 (현황) |
| `WPF_장비UI_이식_계획.md` | semitest ↔ 가상 이송 |
| `PROTO_실행순서.md` | 실행·데모 |
| `PLC_IO_매핑.md` | DI/DO |
| `README.md` | 빌드 |
| `C:\etchflask\ETCH_AI.md` | AI 모델 |
