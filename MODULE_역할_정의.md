# 클러스터 툴 모듈 역할 정의 (현장·가상 공통 기준)

> WPF 도식·`TmTransferSimulator`·모듈 알람/베치 설계의 **단일 기준 문서**.  
> 실장비 연동 범위는 `PROJECT_개요.md` — 가상 장비에서는 아래 **전체 시퀀스**를 소프트웨어로 표현한다.

---

## 1. Load Port 1~3

| 항목 | 내용 |
|------|------|
| **역할** | **FOUP**(Front Opening Universal Pod) 장착. FOUP 안에 웨이퍼 다수(슬롯). |
| **동작** | EFEM·TM이 FOUP에서 **1매씩** 픽업 → Aligner → BM. (확정 루트에서는 **PM 완료 후 FOUP 직행 복귀 없음** — Side Storage 경유.) |
| **가상** | LP1~3 **모두 사용** (3 FOUP 라인). 데모 기본: LP1 FOUP 투입. FOUP 복귀·베치 완료는 스케줄러 확정 후 추가. |

---

## 2. EFEM (대기압)

| 항목 | 내용 |
|------|------|
| **구성** | **EFEM·TM**(대기압 Transfer Module / Robot) + **Aligner** |
| **EFEM·TM** | FOUP → **Aligner(정렬)** → **BM(대기압 투입)**. PM 완료 후 **BM(벤트·대기압)** → **Side Storage**. 만석 시 **다음(외부) 공정** 이송 |
| **Aligner** | 웨이퍼 **노치/방향 정렬** (EFEM·TM이 픽/플레이스) |

도식: EFEM 박스 안 **청록색 EFEM·TM**, BM~PM 구간은 **파란색 진공 TM**.

---

## 3. BM (Buffer Module = Load Lock)

| 항목 | 내용 |
|------|------|
| **역할** | **대기압 ↔ 진공** 전환 공간 |
| **시퀀스** | 대기압에서 웨이퍼 유입 → **진공 전환** → TM이 PM으로 이송 / 역방향 시 **벤트·대기압** 후 EFEM 구간으로 |
| **실장비(벤치)** | **접촉 센서(DI)** = 도어/접촉 닫힘 여부 (가장 중요한 실신호) |

---

## 4. TM (Transfer Module, 진공)

| 항목 | 내용 |
|------|------|
| **역할** | **진공** 클러스터 중앙 TM. BM(진공측) ↔ **PM1~4** 슬릿만 담당 |
| **가상** | `TransferRobotKind.VacuumTm` — BM↔PM 구간에서만 도식 TM·진공 팔 표시 |
| **주의** | FOUP·Aligner·Side Stg는 **EFEM·TM** 담당 (진공 TM 아님) |

---

## 5. PM1~4 (Process Module)

| PM | 공정 | 설명 |
|----|------|------|
| **PM1** | **Strip** | 식각 후 **감광액(Photoresist) 제거** |
| **PM2** | **Etch** | **식각** 공정 챔버 |
| **PM3** | **Etch** | 식각 |
| **PM4** | **Etch** | 식각 |

- 모듈 개념상 Process + Strip 챔버를 가질 수 있으나, **라인 배치상 PM1=Strip 전용, PM2~4=Etch 전용**.
- **알람·레시피·인터락은 PM별로 분리** (공정이 다름).

---

## 6. Side Storage · 다음(외부) 공정

| 항목 | 내용 |
|------|------|
| **Side Storage** | **25매** 버퍼. BM 이후 Fume 제거·적치. |
| **다음 공정 FOUP** | 도식·레거시(`NextProcessFoup*`). **실 이송 없음** — Side Stg **카세트 교체 출하**로 완료 처리. |
| **FOUP 투입** | `FoupPickScheduler`: **잔량 많은 LP 우선**, 동률 LP1→2→3. |

---

## 7. 표준 웨이퍼 루트 (현장 확정 · 가상 이송)

```text
[LP · FOUP 장착]
      │
      ▼  EFEM·TM
[Aligner · 정렬]
      │
      ▼  EFEM·TM
[BM · 대기압 → 진공]
      │
      ▼  진공 TM
[PM2 Etch] → [PM3] → [PM4] → [PM1 Strip]   ← 레시피·순서는 스케줄러
      │
      ▼  진공 TM
[BM · 진공 → 대기압 · EFEM 내부 진입]
      │
      ▼  EFEM·TM
[Side Storage · Fume]
      │
      ├─ (미만석) ──► 다음 매: LP에서 투입 반복
      │
      └─ EFEM·TM ──► [LP별 다음 공정 FOUP]  (Side Stg 25매 만석 시 유입 HOLD)
```

**코드 매핑 (`EquipmentRegion`):**

| Region | 모듈 |
|--------|------|
| `FoupA` | Load Port 1 |
| `FoupB` | Load Port 2 |
| `FoupC` | Load Port 3 |
| `Aligner` | Aligner |
| `EfemRobot` | EFEM 내 대기압 TM 피벗 |
| `LoadLock` | BM |
| `TM` | 진공 TM 피벗 |
| `ChamberA` | PM1 Strip |
| `ChamberB` | PM2 Etch |
| `ChamberC` | PM3 Etch |
| `ChamberD` | PM4 Etch |
| `SideStorage` | Side Storage |
| `NextProcessFoupA/B/C` | LP1~3 다음 공정 FOUP (Side Stg 직결) |

---

## 8. 알람·베치 (향후)

| 항목 | 방향 |
|------|------|
| **알람** | 전역 A001~A006 + **PM1~4·BM·TM·EFEM·LP별 서브 코드** |
| **베치** | FOUP(LP) 단위 / 웨이퍼 슬롯 / “원 LP 복귀” 완료 조건 |
| **레시피** | PM1 Strip 레시피 vs PM2~4 Etch 레시피 분리 |

---

## 9. 구현 상태 (WPF)

| 기능 | 상태 |
|------|------|
| 도식 배치 LP·EFEM·BM·TM·PM1~4 | ✅ |
| 위 **전체 루트** 가상 이송 | ✅ (`TmTransferSimulator` — Side Stg N매·만석→외부) |
| EFEM·TM vs 진공 TM 이중 로봇 | ✅ (`TransferRobotKind`, 도식 2축) |
| PM별 알람·레시피 | 📋 설계만 |
