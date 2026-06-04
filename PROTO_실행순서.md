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



## 데모 체크리스트 (3분 · `PROJECT_계획.md` §6)



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


