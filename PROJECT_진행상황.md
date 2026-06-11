# etch_ui / etchflask — 진행 상황 정리



> **갱신:** 2026-06-04  

> **저장소:** WPF [`yaong832/etch_ui`](https://github.com/yaong832/etch_ui) · Flask **etchflask** (별도, 로컬 `C:\etchflask`)  

> **최근 커밋:** (본 커밋) — 문서 정리·AI 3단계 JSONL·AlarmCatalog 연동



---



## 1. 한 줄 요약



실장비 인터락·버튼·램프와 **가상 TM 이송(듀얼블레이드·가까운 빈 팔)** 이 WPF에서 동작한다. Flask는 텔레메트리·모듈·레시피·**AI 추론**을 담당하며, WPF는 **조언만 표시**한다. **1~2단계 마감** 후 AI 학습·현장 검증이 남았다.



---



## 2. 진행도 (2026-06-04)



| 지표 | 수치 | 비고 |

|------|------|------|

| 로드맵 진행도 | **~82%** | Phase 1~2 WPF·Flask 클라이언트 마감 |

| 제품 완성도 | **~68%** | ML 배포 수동 E2E·TwinCAT 미검 |

| 발표 준비도 | **~88%** | `--sim-*` PASS · B1~B8 수동만 남음 |



---



## 3. 최근 완료 (커밋 기준)



### `c5f7cd8` ~ `9ede039`



| 항목 | 상태 |

|------|------|

| 듀얼블레이드 **가까운 빈 팔** 정책 (`PickNearestFreeBlade`) | ✅ |

| RUNNING 중 블레이드 도식 상시 표시 | ✅ `ca1b6a1` |

| **AlarmCatalog** A001~A006 · 배너·조치·모듈 | ✅ `a16b275` |

| **HmiConnectionPresenter** EtherCAT 칩 통일 | ✅ |

| FOUP LP1~3 잔량·장내 매수·LP별 브러시 | ✅ |

| `HmiTelemetryPublisher` / `HmiFlaskGateway` 분리 | ✅ |

| **EtchTelemetryContractValidator** + sim-smoke | ✅ `9ede039` |

| **HmiFlaskStatusPresenter** 마지막 OK 시각 | ✅ |



### 시뮬·운전 (이전)



| 항목 | 상태 |

|------|------|

| Stop → PauseTransfer / Resume | ✅ |

| `--sim-smoke`, `--sim-stress`, `--sim-aligner-audit` 등 | ✅ PASS |

| Codex Tier 1 HMI (배너·타임라인·hold hint) | ✅ |



---



## 4. 검증



### 자동



| 명령 | 결과 |

|------|------|

| `--sim-smoke` | PASS (AlarmCatalog·Flask 계약 포함) |

| `--sim-stress` | PASS |

| `--sim-aligner-audit` | PASS |

| `--sim-ai-jsonl` | PASS |
| `--sim-flask-e2e --require-ml` | PASS |



### 수동



| 항목 | 문서 |

|------|------|

| B1~B8 발표 | `발표용_데모_체크리스트.md` |

| Flask 웹 E2E | `PROTO_실행순서.md` |

| `train_from_sim.ps1 -Deploy` | `tools/ai/README.md` |

| TwinCAT | 장비 필요 |



---



## 5. Git (etch_ui 최근)



| 커밋 | 요약 |

|------|------|

| `9ede039` | Flask 2단계: payload 계약·FlaskStatus |

| `a16b275` | WPF 1단계: AlarmCatalog·FOUP·Hmi |

| `ca1b6a1` | 블레이드 도식 상시 표시 |

| `c5f7cd8` | 가까운 빈 팔·스케줄러·HMI |



---



## 6. 미완·다음



| 우선 | 항목 |

|:----:|------|

| 1 | WPF 수동 B1~B8 (F5 데모) · 브라우저 KPI 육안 확인 |
| 2 | `MainWindow` PlcPolling / Interlock 분리 |
| 3 | TwinCAT 현장 |
| 4 | 가상/실장 2창 UI (검토 보류) |



---



## 7. 실행·데모



1. (선택) `C:\etchflask\run_flask.bat`

2. F5 → `admin` / `Admin1234` → **시뮬 허용** → **Start**

3. `dotnet run -c Release -- --sim-smoke --ticks=4000`

4. 브라우저 `http://127.0.0.1:5000`



---



## 8. 문서 인덱스



| 문서 | 용도 |

|------|------|

| **PROJECT_진행상황.md** | 본 문서 |

| [**docs/구현상태.md**](docs/구현상태.md) | Phase·Hmi·CLI 요약 |

| `PROJECT_계획.md` | 로드맵 |

| `docs/TODO.md` | 작업 체크리스트 |

| `PROTO_실행순서.md` | 실행·API·E2E |


