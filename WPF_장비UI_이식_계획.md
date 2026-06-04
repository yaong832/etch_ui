# WPF 장비 UI · 가상 이송 (semitest 참고)

> **참고 프로젝트:** `D:\semitest\SemiconductorUi` (WinForms)  
> **대상:** `D:\WPFProject\etch_ui`  
> **전체 로드맵:** [`PROJECT_계획.md`](PROJECT_계획.md)

---

## 1. 목표 (수정됨 — 실장비 전제)

| semitest (WinForms) | etch_ui (WPF) | 상태 |
|---------------------|---------------|------|
| `panelEquipmentCanvas` + GDI+ | `EquipmentSchematicControl` (Viewbox+Canvas) | ✓ |
| `TmVisualizationControl` 16ms | `EquipmentMotionAnimator` 16ms | ✓ |
| `TransferController` + 틱 시뮬 | `TmTransferSimulator` + 1초 `Tick()` | ✓ |
| `Form1TmProcessor` (시뮬 분기) | `MainWindow` RUNNING 시 이송 | ✓ |
| `ChamberController` 공정 시간 | 미구현 | Phase 2 (`PROJECT_계획` §4) |
| FOUP 25슬롯 | `_waferAt` 단순 추적 | Phase 5 |
| **IEG3268** / `TmHardwareController` | **범위 밖** (실 TM 없음) | **제외** |

---

## 2. 실장비 vs 가상 (도어·이송)

| 신호/기능 | semitest 가정 | etch_ui 현장 |
|-----------|---------------|--------------|
| 도어 센서 다수 | Region별 도어 | **접촉 1점 = Load Lock 인터락만** (실측) |
| TM 서보 좌표 | HW 또는 시뮬 | **항상 가상** `TmTransferSimulator` |
| 챔버 도어 열림 | 시뮬 Phase / HW | **가상** `WaitDoorPickupOpen` 등 |

**잘못된 이식 (하지 않음):** 접촉 DI를 Chamber A/B/C 도어에 미러링.

**올바른 이식:** `TransferController.TmPhase` 흐름을 축소해 `SimPhase` + `EquipmentMotionBridge`에 반영.

---

## 3. 현재 구현 (Phase 0~1)

- Load Lock: **접촉 센서** → `LoadLockDoorClosed` only  
- Chamber A/B/C 도어: `TmTransferSimulator.IsVirtualDoorClosed`  
- 기본 루트: `FOUP A → Chamber A → B → C → FOUP B` (루프)  
- 챔버 램프: RUNNING/WARNING/READY 패턴 (`ChamberLampVisual`)  
- 참고 파일: `Services/Simulation/TmTransferSimulator.cs`, `Services/EquipmentMotionBridge.cs`

---

## 4. 다음 이식 (semitest → etch_ui)

| 우선 | semitest 소스 | etch_ui 작업 |
|:----:|---------------|--------------|
| 1 | `Form1UiUpdater` TM phase 라벨 | `ProcessStepLadder` + `PhaseHint` 연동 |
| 2 | `ChamberController.RemainingSeconds` | 가상 챔버 **공정 타이머** (센서와 별개) |
| 3 | `AppSettings` door/tick 상수 | `appsettings.json` `TmPhaseTicks` |
| 4 | `TransferController` 세부 Pickup Phase | 선택(Phase 5) |

---

## 5. 제외 · 별도 계획

| 항목 | 문서 |
|------|------|
| IEG3268, 50ms 서보 폴링 | **하지 않음** |
| AI 추론 | **Flask** `etch_ai.py` — [`PROJECT_계획.md`](PROJECT_계획.md) §10 |
| Storyboard 도어, FOUP 25 UI | Phase 5 |

---

## 6. 발표용 한 줄 (수정)

「semitest의 TM 이송 **시뮬레이션**을 WPF `TmTransferSimulator`로 옮겼고, 실장비는 센서·접촉·버튼·램프만 EtherCAT에 연결하며, AI 진단은 Flask에서 수행한다.」

---

## 관련 문서

- [`PROJECT_계획.md`](PROJECT_계획.md)
- [`PLC_IO_매핑.md`](PLC_IO_매핑.md)
- `D:\semitest\SemiconductorUi\Controllers\TransferController.cs`
- `C:\etchflask\ETCH_AI.md`
