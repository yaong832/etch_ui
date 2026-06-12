# etch_ui

> 📂 [포트폴리오 (Notion)](https://iced-tarsier-455.notion.site/350af40f5e6680d19f93f1653925a3b8) · 🔗 [Flask 서버](https://github.com/yaong832/Flask-etchflask) · 상태: **진행 중**

에칭 Load Lock HMI — WPF(.NET 8) 현장 클라이언트. Flask(`C:\etchflask`)·TwinCAT ADS·가상 TM 이송(`TmTransferSimulator`) 연동.



## 3계층 (요약)



| 계층 | 내용 |

|------|------|

| **실장비** | 센서, **Load Lock 접촉(DI5)**, **버튼 DI0~3**, **램프 DO0~3** |

| **WPF** | 인터락·조작·가상 도식·AI 조언 **표시** (`GET ai/latest`) |

| **Flask** | 원격 조회·이력·**AI 추론** (`etch_ai.py`) |



가상 이송 참고: `D:\semitest\SemiconductorUi` · 상세: [`PROJECT_계획.md`](PROJECT_계획.md)



## 요구 사항



- .NET 8 SDK, Visual Studio 2022 (WPF)

- (선택) TwinCAT ADS

- (선택) **Etch Flask** — `C:\etchflask\run_flask.bat` · `appsettings.json` → `FlaskBaseUrl` (기본 `http://127.0.0.1:5000`)  
  FarmUI(스마트팜)와 별도: [`docs/FLASK_FARMUI_분리.md`](docs/FLASK_FARMUI_분리.md)



## 실행



1. (선택) `C:\etchflask\run_flask.bat` — 미실행 시 HMI만 동작, Flask 상태 **OFF**

2. `etch_ui.sln` → F5 · 로그인 `admin` / `Admin1234`

**발표·PLC 없는 데모**: [`발표용_데모_체크리스트.md`](발표용_데모_체크리스트.md) (3분 스크립트 + 자동 검증 명령)

### TwinCAT 없이 (시뮬만)

1. `appsettings.json` → `"SimulationEnabled": true` 또는 로그인 후 **시뮬 허용** ON

2. **Start**로 가상 이송 확인 · 헤드리스 검증:

```bash
dotnet run -c Release -- --sim-smoke --ticks=4000
dotnet run -c Release -- --sim-ai-jsonl --ticks=120
dotnet run -c Release -- --sim-report --ticks=40000
```

`Services/Hmi/` — Flask 연결·텔레메트리 payload·계약 검증 (`HmiFlaskGateway`, `EtchTelemetryContractValidator` 등).



## 문서

| 문서 | 용도 |
|------|------|
| [**docs/HMI_초보자_가이드.md**](docs/HMI_초보자_가이드.md) | **처음 사용자** — 화면 구성·분리 창·**외부 모니터** |
| [**docs/FLASK_초보자_가이드.md**](docs/FLASK_초보자_가이드.md) | **Flask 웹** — 탭별 설명·demo/live·AI 진단 |
| [**PROJECT_계획.md**](PROJECT_계획.md) | **전체 로드맵** (Phase 0~5, AI §10) |
| [PROJECT_진행상황.md](PROJECT_진행상황.md) | 진행도·최근 커밋 |
| [docs/구현상태.md](docs/구현상태.md) | Phase·Hmi·CLI 요약 |
| [PROJECT_개요.md](PROJECT_개요.md) | 현황 스냅샷 |
| [`docs/TODO.md`](docs/TODO.md) | Flask 제외, 지금 할 수 있는 작업 목록 |
| [PROTO_실행순서.md](PROTO_실행순서.md) | 실행·데모·API |
| [PLC_IO_매핑.md](PLC_IO_매핑.md) | DI/DO |
| [WPF_장비UI_이식_계획.md](WPF_장비UI_이식_계획.md) | semitest ↔ 가상 이송 |
| [SCHEDULER_FOUP_PM_정책.md](SCHEDULER_FOUP_PM_정책.md) | FOUP·PM·Side·듀얼 블레이드·헤드리스 시뮬 |
| `C:\etchflask\ETCH_AI.md` | AI 모델·API |
| [tools/ai/README.md](tools/ai/README.md) | 로컬 AI 학습 데이터 수집/변환/베이스라인 |
| [docs/FLASK_E2E.md](docs/FLASK_E2E.md) | Flask HTTP E2E (`--sim-flask-e2e`) |

## 설정



`appsettings.json` — Flask URL, ADS 포트, `SimulationEnabled`, `Interlock`, `PressureScale`, `ProcessRecipe`

Flask 이력 DB: `C:\etchflask\data\etch_monitoring.db` · WPF 로컬: `data/etch_hmi.db` (`telemetry_samples`)


