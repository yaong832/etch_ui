# etch_ui



에칭 Load Lock HMI — WPF(.NET 8) 현장 클라이언트. Flask(`C:\etchflask`)·TwinCAT ADS·가상 TM 이송(`TmTransferSimulator`) 연동.



## 3계층 (요약)



| 계층 | 내용 |

|------|------|

| **실장비** | 센서, **Load Lock 접촉(DI5)**, **버튼 DI0~3**, **램프 DO0~3** |

| **WPF** | 인터락·조작·가상 도식·(예정) AI 조언 **표시** |

| **Flask** | 원격 조회·이력·**AI 추론** (`etch_ai.py`) |



가상 이송 참고: `D:\semitest\SemiconductorUi` · 상세: [`PROJECT_계획.md`](PROJECT_계획.md)



## 요구 사항



- .NET 8 SDK, Visual Studio 2022 (WPF)

- (선택) TwinCAT ADS

- (선택) Flask — `appsettings.json` → `FlaskBaseUrl` (기본 `http://127.0.0.1:5000`)



## 실행



1. `C:\etchflask\run_flask.bat`

2. `etch_ui.sln` → F5 · 로그인 `admin` / `Admin1234`



## 문서

| 문서 | 용도 |
|------|------|
| [**PROJECT_계획.md**](PROJECT_계획.md) | **전체 로드맵** (Phase 0~5, AI §10) |
| [PROJECT_개요.md](PROJECT_개요.md) | 현황 스냅샷 |
| [`docs/TODO.md`](docs/TODO.md) | Flask 제외, 지금 할 수 있는 작업 목록 |
| [PROTO_실행순서.md](PROTO_실행순서.md) | 실행·데모·API |
| [PLC_IO_매핑.md](PLC_IO_매핑.md) | DI/DO |
| [WPF_장비UI_이식_계획.md](WPF_장비UI_이식_계획.md) | semitest ↔ 가상 이송 |
| [SCHEDULER_FOUP_PM_정책.md](SCHEDULER_FOUP_PM_정책.md) | FOUP·PM·Side·듀얼 블레이드·헤드리스 시뮬 |
| `C:\etchflask\ETCH_AI.md` | AI 모델·API |
| [tools/ai/README.md](tools/ai/README.md) | 로컬 AI 학습 데이터 수집/변환/베이스라인 |

## 설정



`appsettings.json` — Flask URL, ADS 포트, `SimulationEnabled`, `Interlock`, `PressureScale`


