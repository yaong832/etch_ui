# EtherCAT / TwinCAT I/O 매핑 (식각 HMI)

> 스마트팜 WinForm(`D:\vs\vs\farm`, `Lightonoff`)과 동일한 GVL 심볼 기준  
> 코드: `Plc\PlcAdsService.cs`, `Plc\AdsPlcTypes.cs`

## ADS 심볼

| 구분 | TwinCAT 심볼 | 대체 심볼 |
|------|----------------|-----------|
| 아날로그 입력 | `GVL.NX_AD4203` | — |
| 디지털 입력 | `GVL.NX_ID5342` | `NX_ID5342` |
| 디지털 출력(램프) | `GVL.NX_OD5121` | `NX_OD5121` |

## 디지털 입력 (`NX_ID5342`.Bits)

| 비트 | 신호 | HMI 용도 |
|:----:|------|----------|
| 0 | Button 1 | Start (엣지) |
| 1 | Button 2 | Stop |
| 2 | Button 3 | Alarm Reset |
| 3 | Button 4 | Maintenance |
| 4 | 광학 센서 (Fiberoptic) | *(HMI 미사용)* |
| **5** | **접촉 (Inductive_Sensor)** | **Load Lock 문 닫힘 / 인터락 (A004)** |

> 비트5 = **실장비 접촉 1점**(Load Lock). Chamber A/B/C 도어는 `TmTransferSimulator` **가상 이송** 단계에서만 열림 표시.

### Load Lock 접촉 센서 (비트 5)

- PLC/실습 코드: `Inductive_Sensor = (Bits & (1<<5)) != 0`
- HMI: **비트5 true → 닫힘 → 정상 가동 가능 (`AccessSafe=true`)**
- HMI: **비트5 false → 열림 → 인터락 미충족, A004**
- DI 모듈을 읽지 못하면 `AccessInputValid = false` → 화면 **「—」**, Start 불가

## 디지털 출력 (램프)

| 비트 | 램프 |
|:----:|------|
| 0 | READY |
| 1 | RUNNING |
| 2 | WARNING |
| 3 | ALARM |

## 아날로그 입력 (`NX_AD4203`)

| 필드 | 물리량 | 비고 |
|------|--------|------|
| PressureSensor | 압력 (mTorr) | `PlcAnalogScaling.TryPressureMtorr` |
| VibrationSensor | 진동 (%) → g | `VibrationPercentToG` |
| TemperatureSensor | 온도 ℃ | |
| HumiditySensor | 습도 % | |

## 관련 문서

- [구현 상태 체크리스트](d:/wpf과제프로젝트/구현_상태_체크리스트.md)
- [PROTO 실행순서](PROTO_실행순서.md)
- [원격 모니터링 (Flask)](C:/etchflask/REMOTE_MONITORING.md)
