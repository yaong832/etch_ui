# 식각 HMI 프로토타입 실행 순서



## 관련 문서



| 문서 | 경로 |

|------|------|

| **전체 계획** | [`PROJECT_계획.md`](PROJECT_계획.md) |

| **현황** | [`PROJECT_개요.md`](PROJECT_개요.md) |

| **PLC I/O** | [`PLC_IO_매핑.md`](PLC_IO_매핑.md) (접촉=Load Lock DI bit5) |

| **가상 이송** | [`WPF_장비UI_이식_계획.md`](WPF_장비UI_이식_계획.md) (`D:\semitest` 참고) |

| **AI** | `C:\etchflask\ETCH_AI.md` |

| **원격 모니터링** | `C:\etchflask\REMOTE_MONITORING.md` |

| **Flask README** | `C:\etchflask\README.md` |
| **HMI 초보자** | [`docs/HMI_초보자_가이드.md`](docs/HMI_초보자_가이드.md) (분리 창·외부 모니터) |
| **Flask 초보자** | [`docs/FLASK_초보자_가이드.md`](docs/FLASK_초보자_가이드.md) (웹 탭·AI) |
| **발표·데모 (PLC 없음)** | [`발표용_데모_체크리스트.md`](발표용_데모_체크리스트.md) |



## 2대 PC 구성 (권장)



| PC | 할 일 |

|----|--------|

| **현장 PC** | EtherCAT + WPF + Flask (`run_flask.bat`) |

| **모니터링 PC** | 브라우저 → `http://<현장PC IP>:5000` (조회·AI 탭) |



## 1) Flask — 현장 PC



1. `C:\etchflask\run_flask.bat`  

2. `http://127.0.0.1:5000` — 실시간 / 이력 / 이벤트 / **AI 진단** 탭  

3. 모니터링 PC: `http://<현장IP>:5000` · TCP **5000** 허용  



### API



| API | 용도 |

|-----|------|

| `GET /api/sensors` | 스냅샷·헬스 |

| `POST /api/etch/sensor-data` | WPF 텔레메트리 (~2초) |

| `GET /api/etch/history` · `events` · `summary` | 이력 |

| `GET /api/etch/modules/latest?source=demo\|live` | 모듈 상태 (WPF `modules[]`) |

| `GET /api/etch/recipe/active?source=demo\|live` | 활성 레시피 (WPF `recipe`) |

| `GET /api/etch/ai/status` | AI 모델 상태 |

| `POST /api/etch/ai/predict` | 추론 (스텁/실모델) |

| `GET /api/etch/ai/latest` | WPF AI 조언 폴링 (~6초) |



## 2) WPF HMI — 현장 PC



1. `D:\WPFProject\etch_ui\etch_ui.sln` → F5  

2. 로그인: `admin` / `Admin1234`  

3. EtherCAT Connected → **실측** 센서 표시 (미연결 시 **—**)  

4. `FlaskBaseUrl` = `http://127.0.0.1:5000` (현장 PC 로컬)  



### 조작



| 입력 | 동작 |

|------|------|

| **접촉 닫힘** | Load Lock 인터락 — Start 가능 조건 |

| **Start** | RUNNING + **가상 TM 이송** 시작 |

| **Stop** | 정지 + 가상 이송 정지 |

| **Reset / Maint** | 관리자, 알람 리셋·유지보수 |

| **HW DI0~3** | UI와 동일 (Start/Stop/Reset/Maint) |



## 설정 (`appsettings.json`)



- `FlaskBaseUrl`, `AdsPort`, `SimulationEnabled`, `Interlock`, `PressureScale`



## 실장비 vs 가상



| 신호 | 실제 | HMI |

|------|:----:|-----|

| 압력·진동·온·습도 | ✓ | 인터락·표시·Flask·**AI** |

| 접촉 DI5 | ✓ | **Load Lock 인터락만** (A004) |

| 버튼 DI0~3 | ✓ | Start/Stop/Reset/Maint |

| 램프 DO0~3 | ✓ | 상태 **출력** |

| TM·챔버·FOUP | ✗ | `TmTransferSimulator` (`D:\semitest` 참고) |



> AI는 Flask에서 추론. WPF·웹은 **조언 표시만** — 인터락·Start 자동 변경 없음.



## 화면 구역



| 영역 | WPF | Flask 웹 |

|------|-----|----------|

| 주 | 5:3:2.2 (도식·인터락·**예정 AI**·램프·버튼) | KPI·차트·가상 요약 |

| 로그 | 하단 ~140px | 하단 스트립 ~132px |

| AI | 중앙 **AI 조언** 패널 | **AI 진단** 탭 |
| 이벤트 | 헤더 **이벤트 로그** 버튼 | Flask events 탭 |



## 데모 체크리스트



### 발표 PC (PLC·TwinCAT 없음) — **권장**

전체 순서·3분 스크립트·자동 검증 명령: **[`발표용_데모_체크리스트.md`](발표용_데모_체크리스트.md)**

- [ ] Flask `run_flask.bat` · WPF 로그인 · **시뮬 허용** ON  
- [ ] **데모 진행** 또는 **Start** → 가상 TM·FOUP·모듈 표시  
- [ ] Flask 웹: 실시간 · 모듈 · 레시피 · **AI 진단**  
- [ ] **Stop** — 가상 이송 **일시정지** (FOUP·슬롯 유지) · **Start** 재개 / LOT 완료 후 Start는 새 LOT  
- [ ] (관리자) **Maint** → **정비 도구** — 가상 슬롯·FOUP·1틱 이송 (`MaintenanceToolsWindow`)  
- [ ] (선택) `dotnet run -- --sim-smoke --ticks=5000`  

### 현장 PC (실장비 · `PROJECT_계획.md` §6)

- [ ] Flask OK, WPF EtherCAT Connected, 실측 센서  
- [ ] 접촉 닫힘 → 인터락 OK → Start → 가상 TM 이동  
- [ ] (Phase 1.2) 접촉 열림 → ALARM, 이송 정지  
- [ ] 모니터링 PC Flask 실시간 + AI 탭  
- [ ] Stop / Reset — AI는 조언만  



## HW·UI 버튼 매트릭스 (Phase 1.3)



| 동작 | UI | DI | 권한 |

|------|:--:|:--:|------|

| Start | ✓ | 0 | 작업자+ |

| Stop | ✓ | 1 | 작업자+ |

| Reset | ✓ | 2 | 관리자 |

| Maint | ✓ | 3 | 관리자 |



## 헤드리스 시뮬 (정책·LOT 회귀)



WPF UI 없이 스케줄·상태만 검증:



```powershell
cd D:\WPFProject\etch_ui
dotnet run -- --sim-smoke --ticks=5000
dotnet run -- --sim-policy-batch --runs=10 --ticks=5000
dotnet run -- --sim-maintenance --ticks=800
dotnet run -- --sim-dual-blade --ticks=12000
dotnet run -- --sim-report --ticks=250000
```

| Stop / Start | 동작 |
|--------------|------|
| **Stop** | `PauseTransfer()` — 로봇 큐·FOUP·BM 상태 **유지** |
| **Start** (일시정지 직후) | `ResumeTransfer()` — 이어서 운전 |
| **Start** (LOT 완료·초기) | `StartDemoLoop()` — 데모 초기화 후 **새 LOT** |
| 정비 **전체 초기화** | `MaintenanceResetVirtualLine()` / `ResetDemoLine()` |



| 항목 | 설명 |
|------|------|
| LOT 75 | Side Stg 25매 × **3회** 카세트 출하 후 완료 (`LotCompletionTracker`) |
| `lot=0/75` 장시간 | tick 부족·시뮬 처리량 이슈 가능 — **현장 상시 정체와 동일하지 않음** (상세 `SCHEDULER_FOUP_PM_정책.md` §5) |
| 확인 우선순위 | ① smoke/batch 성공 ② UI RUNNING ③ 필요 시 `--ticks` 확대 |



## 스케줄·정책 문서



- [`SCHEDULER_FOUP_PM_정책.md`](SCHEDULER_FOUP_PM_정책.md) — FOUP·PM·Side·듀얼 블레이드·시뮬 (코드와 동기화)


