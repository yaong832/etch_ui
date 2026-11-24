# etch_ui - 플라즈마 식각 Load Lock HMI (WPF)

> 📂 [포트폴리오 (Notion)](https://iced-tarsier-455.notion.site/350af40f5e6680d19f93f1653925a3b8) · 🔗 [Flask 서버 (Flask-etchflask)](https://github.com/yaong832/Flask-etchflask) · 상태: **완료 (2학년 1학기)** · 남은 과제: TwinCAT 실장비 현장 검증

식각 장비 Load Lock의 현장 HMI입니다. WPF(.NET 8)로 만들었고, TwinCAT ADS로 센서·버튼·램프를 읽고 쓰며, 가상 TM 이송(`TmTransferSimulator`)과 [Flask 모니터링 서버](https://github.com/yaong832/Flask-etchflask) 연동을 포함합니다.

## 3계층

| 계층 | 내용 |
| --- | --- |
| **실장비** | 센서, **Load Lock 접촉(DI5)**, **버튼 DI0~3**, **램프 DO0~3** |
| **WPF (이 저장소)** | 인터락·조작·가상 도식·AI 조언 **표시** (`GET ai/latest`) |
| **Flask** | 원격 조회·이력·**AI 추론** ([Flask-etchflask](https://github.com/yaong832/Flask-etchflask)) |

가상 이송은 [SemiconductorUi](https://github.com/yaong832/SemiconductorUi)의 TM 이송 구조를 참고했습니다. 상세: [PROJECT_계획.md](PROJECT_계획.md)

## 주요 기능

- **3단계 인터락**: 압력·진동·온도·습도를 정상 / 경고 / 알람으로 판정 (`Services/InterlockSeverityEvaluator.cs`, 기준값은 `appsettings.json`의 `Interlock`)
- **Load Lock 접촉 센서(DI5)**: 문이 열려 있거나 DI를 못 읽으면 Start 불가
- **가상 TM 이송·스케줄링**: FOUP·PM·듀얼 블레이드 정책 (`Services/Scheduling/`, [SCHEDULER_FOUP_PM_정책.md](SCHEDULER_FOUP_PM_정책.md))
- **Flask 연동**: 텔레메트리 전송, AI 진단 조언 표시, 연결 상태 표시 (`Services/Hmi/`)
- **사용자·권한**: 관리자 재인증, 비밀번호는 PBKDF2(SHA-256, 10만 회) + 솔트로 저장 (`Security/`)
- **헤드리스 자동 검증**: 화면 없이 시뮬레이션을 돌려 결과를 검사하는 명령 13종 (아래)

---

## 요구 사항

- .NET 8 SDK, Visual Studio 2022 (WPF)
- (선택) TwinCAT ADS. 없으면 시뮬레이션으로 동작
- (선택) [Flask-etchflask](https://github.com/yaong832/Flask-etchflask) 서버. `appsettings.json`의 `FlaskBaseUrl` (기본 `http://127.0.0.1:5000`)
  스마트팜 Flask와는 별개입니다: [docs/FLASK_FARMUI_분리.md](docs/FLASK_FARMUI_분리.md)

## 실행

1. (선택) Flask 서버 실행 (Flask-etchflask의 `run_flask.bat`). 실행하지 않으면 HMI만 동작하고 Flask 상태는 **OFF**
2. `etch_ui.sln`을 열고 F5. 처음 관리자 계정은 `admin` / `Admin1234`입니다. 실제로 쓸 때는 바로 바꾸세요.

**발표·PLC 없는 데모**: [발표용_데모_체크리스트.md](발표용_데모_체크리스트.md) (3분 스크립트 + 자동 검증 명령)

### TwinCAT 없이 (시뮬레이션만)

1. `appsettings.json` → `"SimulationEnabled": true`, 또는 로그인 후 **시뮬 허용** ON
2. **Start**로 가상 이송 확인

### 헤드리스 자동 검증

화면을 띄우지 않고 시뮬레이션을 돌려 결과를 검사합니다.

```bash
dotnet run -c Release -- --sim-smoke --ticks=4000
dotnet run -c Release -- --sim-ai-jsonl --ticks=120
dotnet run -c Release -- --sim-report --ticks=40000
```

전체 명령: `--sim-smoke`, `--sim-report`, `--sim-stress`, `--sim-policy-batch`, `--sim-dual-blade`, `--sim-efem-audit`, `--sim-aligner-audit`, `--sim-alarm`, `--sim-maintenance`, `--sim-ui-hints`, `--sim-app-settings`, `--sim-ai-jsonl`, `--sim-flask-e2e` ([docs/FLASK_E2E.md](docs/FLASK_E2E.md))

---

## 문서

| 문서 | 용도 |
| --- | --- |
| [**docs/HMI_초보자_가이드.md**](docs/HMI_초보자_가이드.md) | **처음 사용자**: 화면 구성, 분리 창, 외부 모니터 |
| [**docs/FLASK_초보자_가이드.md**](docs/FLASK_초보자_가이드.md) | **Flask 웹**: 탭별 설명, demo/live, AI 진단 |
| [**PROJECT_계획.md**](PROJECT_계획.md) | **전체 로드맵** (Phase 0~5, AI §10) |
| [PROJECT_진행상황.md](PROJECT_진행상황.md) | 진행도, 최근 커밋 |
| [docs/구현상태.md](docs/구현상태.md) | Phase, Hmi, CLI 요약 |
| [PROJECT_개요.md](PROJECT_개요.md) | 현황 스냅샷 |
| [docs/TODO.md](docs/TODO.md) | Flask 제외, 지금 할 수 있는 작업 목록 |
| [PROTO_실행순서.md](PROTO_실행순서.md) | 실행, 데모, API |
| [PLC_IO_매핑.md](PLC_IO_매핑.md) | DI/DO |
| [WPF_장비UI_이식_계획.md](WPF_장비UI_이식_계획.md) | SemiconductorUi ↔ 가상 이송 |
| [SCHEDULER_FOUP_PM_정책.md](SCHEDULER_FOUP_PM_정책.md) | FOUP, PM, Side, 듀얼 블레이드, 헤드리스 시뮬 |
| [ETCH_AI.md (Flask 저장소)](https://github.com/yaong832/Flask-etchflask/blob/master/ETCH_AI.md) | AI 모델, API |
| [tools/ai/README.md](tools/ai/README.md) | 로컬 AI 학습 데이터 수집·변환·베이스라인 |
| [docs/FLASK_E2E.md](docs/FLASK_E2E.md) | Flask HTTP E2E (`--sim-flask-e2e`) |

## 설정

`appsettings.json`: Flask URL, ADS 포트, `SimulationEnabled`, `Interlock`, `PressureScale`, `ProcessRecipe`

- WPF 로컬 DB: `data/etch_hmi.db` (`telemetry_samples`)
- Flask 이력 DB: Flask 저장소의 `data/etch_monitoring.db` (`ETCH_USE_DB=1`일 때)
