# WPF 장비 UI 이식 계획 (SemiconductorUi 챔버램프수정 기준)

> 기준 프로젝트: `2504110105_Final2025(챔버램프수정)`  
> 대상: `D:\WPFProject\etch_ui`

## 1. 목표

| WinForms (기존) | WPF (목표) |
|-----------------|------------|
| `panelEquipmentCanvas` + GDI+ | **Viewbox + Canvas** (벡터, 스케일) |
| `TmVisualizationControl` 16ms | `EquipmentMotionAnimator` 16ms 보간 |
| Phase(로직) / HW 폴링(50ms) 분리 | `EquipmentMotionBridge` + 1초 UI 타이머 |
| IEG3268 서보 X/Y | **2단계:** IEG3268 DLL 연동 |
| 챔버 램프 깜빡임 v2.1 | `ChamberLampVisual` + Bridge |

## 2. 현재 구현됨 (1단계)

- `Equipment/Views/EquipmentSchematicControl` — TM·Chamber A/B/C·FOUP·Load Lock 배치
- `EquipmentMotionViewModel` — 블레이드 각·신장·도어·램프·웨이퍼 표시
- **실시간 동기화 (논리)**
  - 유도형 도어 → Load Lock / Chamber 도어 표시
  - `RUNNING` → TM이 Chamber 방향 + 신장 1.3
  - `WARNING` → 챔버 A 램프 깜빡임 (수정본 로직 단순화)
  - `READY` → Chamber B 완료 깜빡임
- 시뮬 RUNNING 시 TM 경로 데모 (A→B→C→FOUP 순환)

## 3. 2단계 (하드웨어 실시간)

1. `IEG3268_Dll.dll` 참조 (SemiconductorUi와 동일)
2. `TmHardwareController` 포팅 → `Ieg3268MotionService`
3. 50ms 폴링: `Axis1`/`Axis2` → `DetermineRegionFromPosition`
4. 실린더 센서 → `BladeExtension` (1.3 / 0.55 / 0.7)
5. 진공 ON → `CarryingWafer`
6. `appsettings.json`: `UseIeg3268Motion: true`

## 4. 3단계 (WPF 강점)

- **Storyboard** 도어 개폐 애니메이션
- **DataTemplate** FOUP 25슬롯
- **Transfer 시퀀스** `TmPhase` 바인딩 (WinForms `TransferController` 이식)
- **Vector 장비 개략도** (확대·테마·고해상도 모니터)

## 5. 폴더 구조

```
etch_ui/Equipment/
  Models/          EquipmentRegion, ChamberLampVisual
  Layout/          설계 좌표 (티칭값 X는 2단계)
  Helpers/         RegionAngleHelper
  ViewModels/      EquipmentMotionViewModel
  Views/           EquipmentSchematicControl
  Converters/      도어 색/텍스트
Services/
  EquipmentMotionBridge.cs
  EquipmentMotionAnimator.cs
```

## 6. 발표 시 한 줄

「WinForms SemiconductorUi의 TM·챔버 배치와 챔버 램프 수정본을 WPF Viewbox 도식으로 이식했고, EtherCAT 센서·도어와 연동하며, 서보 좌표 동기화는 IEG3268 연동으로 확장 예정」

## 관련 문서

- `구현_상태_체크리스트.md` (과제 폴더)
- `PLC_IO_매핑.md`
- SemiconductorUi `코드_상세_설명_챔버_램프_로직_업데이트.md`
