# 장비 로직·스케줄러·레시피 설계 (초안)

> **전제:** [`MODULE_역할_정의.md`](MODULE_역할_정의.md) 공정 흐름  
> **상태:** 용량·알고리즘 **미확정** — 아래 표의 “결정 필요” 항목을 채우면 스케줄러·레시피·가상 이송 순서가 정해짐  
> **현재 WPF:** `FoupPickScheduler` + `TmTransferSimulator` (FOUP 25×3, Side Stg 25, 다음 FOUP 직결, PM tick) — **전역 스케줄러·레시피 엔진은 미구현**  
> **정책 상세:** [`SCHEDULER_FOUP_PM_정책.md`](SCHEDULER_FOUP_PM_정책.md)

---

## 1. 말씀하신 라인 구조 (목표)

```text
[LP1~3 · FOUP] ──EFEM·TM(대기압)──► Aligner ──► BM
                                              │
                                    [TM · 진공]
                                              │
                         PM2, PM3, PM4 (식각 Etch) ──► PM1 (식각 후 Strip)
                                              │
                                    BM ──► Side Storage (Fume 제거·버퍼)
                                              │
                         Side Stg(25) ──► LP별 **다음 공정 FOUP** (직결)  ← **현장 확정**
                                              │
                         FOUP 투입 우선순위 ──► [`SCHEDULER_FOUP_PM_정책.md`](SCHEDULER_FOUP_PM_정책.md) §1
```

| 블록 | 역할 |
|------|------|
| **FOUP ×3** | 투입·완료 FOUP. 스케줄러가 “어느 LP에서 꺼낼지/어디로 돌릴지” 결정 |
| **PM2~4** | 식각 (레시피·순서·병렬 여부는 스케줄러) |
| **PM1** | Strip (감광 제거) — **식각 PM 통과 후** |
| **Side Storage** | 복귀 전 Fume 제거 + **완료 웨이퍼 임시 적치**. **만석 시 외부 이송** |
| **TM / EFEM·TM** | 용량(1매 vs 2매)에 따라 **이송 알고리즘 전체가 달라짐** |

---

## 2. 먼저 정해야 하는 용량 파라미터 (결정 필요)

아래 값이 정해지기 전에는 **스케줄러·TM 알고리즘·레시피를 확정할 수 없음**.

| ID | 항목 | 질문 | 일반적 장비 | 가상 장비 제안(초안) | 결정 |
|----|------|------|-------------|----------------------|------|
| **C1** | FOUP 슬롯/매수 | LP당 최대 몇 매? | 13 / 25 / 26 | **25** | ☑ |
| **C2** | LP 동시 FOUP | 3 LP에 FOUP 동시 적재? | 3 FOUP 동시 가능 | **3 FOUP 동시** | ☐ |
| **C3** | BM 수용 | Load Lock에 동시 몇 매? | **1매** (일반 LL) | **1매** | ☐ |
| **C4** | TM 블레이드 | 1매 vs 2매 동시 이송? | 대부분 **1매**; 일부 장비 dual arm | **1매** (1차) / dual은 Phase 2 | ☐ |
| **C5** | PM 수용 | PM당 동시 몇 매? | **1매/챔버** | **1매** | ☐ |
| **C6** | PM 내부 | Process+Strip 동시 vs PM 분리? | 라인마다 다름 | **PM1=Strip 전용, PM2~4=Etch** (확정) | ☑ |
| **C7** | Side Storage | 최대 몇 매? | 설비 스펙 | **25** | ☑ |
| **C8** | Side Stg 만석 | 만석 시 동작 | 다음 FOUP 직결 | **25 만석 시 BM→Side Stg HOLD** | ☑ |
| **C9** | Aligner | 동시 1매? | **1매** | **1매** | ☐ |
| **C10** | 식각 PM 순서 | PM2→3→4 고정? 레시피별? | 레시피/레시피 | **레시피 스텝** (초기는 2→3→4 고정) | ☐ |

**핵심:** **C4(TM 1 vs 2매)** 와 **C7~C8(Side Storage)** 이 스케줄러 복잡도에 가장 큰 영향.

---

## 3. 용량별로 달라지는 것

### 3.1 TM = 1매 (단일 블레이드/단일 그리퍼)

- BM·PM·Aligner 모두 **“한 번에 한 매”** 가정과 잘 맞음.
- **병렬:** PM2 식각 중에 PM3에 넣을 수 **없음** (TM이 한 대이므로 순차).
- 스케줄러 = **순차 Job + 리소스 점유** (BM, TM, 각 PM, Aligner, Side Stg 슬롯).
- 가상 이송: 지금 `TmTransferSimulator` 구조와 **동일 계열**.

### 3.2 TM = 2매 (dual arm / dual blade)

- 이론상: 한 PM에서 pick 하는 동안 다른 arm으로 다른 PM/ BM 작업 **가능** (장비 interlock·소프트 충돌 방지 필요).
- 스케줄러 = **2-token TM** + 충돌 없는 **이동 매트릭스**.
- WPF: 블레이드 2개 또는 “논리 슬롯 2” 표현 필요 → **Phase 2**.

### 3.3 FOUP 25매 × 3

- 스케줄러 입력: `(LP, slotIndex)` 큐.
- **원 FOUP 복귀:** 웨이퍼마다 `OriginLp`, `OriginSlot` 저장 필수.
- 3 FOUP **동시 가동** 시: EFEM·BM·TM·PM이 병목 — **스케줄러가 우선순위** (FIFO / LP 라운드로빈 / 긴급 Lot).

### 3.4 BM 1매

- **대기압↔진공 전환 중** 다른 웨이퍼 BM 진입 **불가** (일반적).
- 스케줄러: BM을 **상호 배제 자원(Mutex)** 로 모델링.
- EFEM·TM(대기압)이 BM에 넣은 뒤 **pump down 완료** 전까지 TM(진공) pick 금지.

### 3.5 PM 1매 × 4 · PM2~4 동일 Etch

- **PM2·3·4:** 동일 식각 레시피 → 챔버에 웨이퍼가 있으면 **병렬 식각** 가능 (TM 1매는 이송만 순차).
- **PM1:** 2~4 **후가공 Strip**, 레시피 시간 **Etch보다 짧게** (시뮬: Strip 12 tick / Etch 30 tick, 튜닝).
- 1매 TM 데모 경로: `PM2→PM3→PM4→PM1` 순차 이송 + 구간별 `WaitProcess`.

### 3.6 Side Storage N매 + 외부 이송

- **버퍼 큐:** 완료 웨이퍼가 FOUP 복귀 전 머무는 곳.
- **만석 정책 (예시):**
  - `Count >= C7` → **ExternalTransfer** Job 생성 (EFEM·TM이 Storage→외부 포트).
  - 신규 **FOUP 투입 / 식각 완료 유입** HOLD (또는 Storage만 비울 때까지).
- FOUP 복귀와 외부 이송 **순서:**  
  - A) Storage → 외부 **먼저**, 빈 슬롯 → FOUP 복귀  
  - B) FOUP 복귀 **먼저**, Storage는 “출하 대기”만  
  → **C8로 결정 필요**.

---

## 4. 알고리즘 계층 (구현 순서 제안)

```text
[Layer 0] 용량·리소스 모델 (C1~C10 확정)
    ↓
[Layer 1] 웨이퍼 Lot/슬롯·상태 머신 (Idle, AtAligner, InBm, InPm2, …, InSideStg, Done)
    ↓
[Layer 2] 스케줄러 — Job 큐·리소스 Mutex·Side Stg 만석 정책
    ↓
[Layer 3] 이송 플래너 — 다음 (pickup, dropoff, robot=EFEM|Vacuum) 한 건
    ↓
[Layer 4] TmTransferSimulator / 실 TM·EFEM — Layer 3 명령 실행
    ↓
[Layer 5] 레시피 엔진 — PM 내부 Process/Strip 스텝·타이머·알람
```

| Layer | 산출물 | etch_ui 현재 |
|-------|--------|--------------|
| 0 | `EquipmentCapacityConfig` (json) | 없음 |
| 1 | `Wafer` record + 상태 | 없음 (HasWaferAt region만) |
| 2 | `ClusterScheduler` | 없음 |
| 3 | `TransferPlanner` | 없음 (고정 큐) |
| 4 | 시뮬/PLC | 데모 큐만 |
| 5 | `Recipe` per PM | 없음 |

---

## 5. TM 이송 알고리즘 (1매 TM 가정, 초안)

한 **TransferJob** = `(robot, from, to, waferId)`.

**진공 TM 허용 구간**

- BM ↔ PM2, PM3, PM4, PM1 (레시피 스텝 순서 따름)
- PM 간 직접 이송 **없음** (항상 TM 경유)

**EFEM·TM 허용 구간**

- LP(i) ↔ Aligner ↔ BM
- BM ↔ Side Storage (벤트 후 대기압)
- Side Storage ↔ 외부 포트 (C8)
- Side Storage ↔ LP(i) (원 FOUP 복귀)

**BM 인터락 (의사코드)**

```text
on EFEM place to BM (atm):
  wait BM door closed, contact OK
  BM.mode = Atmospheric, slot = wafer

on PumpDownComplete:
  BM.mode = Vacuum

on Vacuum TM pick from BM:
  require BM.mode == Vacuum && BM.slot == wafer

on Vacuum TM place to BM after PM1:
  BM.slot = wafer; schedule VentWhenReady

on VentComplete:
  BM.mode = Atmospheric
  allow EFEM pick to SideStorage or LP
```

**스케줄러가 TM에게 내리는 일**

- 동시에 **한 TransferJob만 active** (TM 1매).
- PM `Processing` 중이면 해당 PM 슬릿 **pick 금지**.
- Side Storage 만석이면 `to=SideStorage` Job **금지** (대신 ExternalDrain 우선).

---

## 6. 레시피 (Layer 5) — 스케줄러와 분리

| PM | 레시피 종류 | 예시 스텝 |
|----|-------------|-----------|
| PM2~4 | `EtchRecipe` | Gas, RF, time, pressure setpoint |
| PM1 | `StripRecipe` | Strip time, chemistry |

- 스케줄러: “**언제** PM2에 넣을지”  
- 레시피: “**넣은 뒤** 몇 초/어떤 조건”  
- PM `Processing` → 레시피 실행 중 → `Complete` → TM pick 허용

---

## 7. 가상 장비 1차 목표 (합의 후 코딩)

| 단계 | 내용 |
|------|------|
| **S0** | `appsettings` 또는 `EquipmentCapacity.json` — C1~C10 기본값 |
| **S1** | `Wafer` + `OriginLp/Slot` + 3 FOUP 슬롯 맵 (단순 bool[25]×3) |
| **S2** | `ClusterScheduler` — 1매 TM, BM mutex, SideStg N슬롯, 만석→외부 Job |
| **S3** | `TransferPlanner` → `TmTransferSimulator` 큐 **동적 생성** (고정 BuildStandardCycle 제거) |
| **S4** | PM 레시피 타이머 스텁 (Etch 30s, Strip 20s 등) |
| **S5** | WPF: 슬롯·Storage occupancy·스케줄 로그 패널 |

---

## 8. 확인 질문 (다음 회의/답변용)

1. **TM 1매 vs 2매** — 1차 가상은 1매로 갈까요?  
2. **FOUP** — LP당 **25매** 가정해도 될까요?  
3. **Side Storage** — 최대 **몇 매**? 만석 시 **FOUP 복귀를 막고** 외부만 할까요, 둘 다 병행할까요?  
4. **식각 PM** — 항상 2→3→4 **전부** 거치나요, 레시피마다 **부분 집합**인가요?  
5. **외부 공정** — 도식에 **포트 1개**만 표시할까요 (예: “External”)?

---

## 9. 한 줄 결론

- **맞습니다:** 스케줄러·실제 가공 로직·레시피는 **아직 없고**, 지금은 **용량 가정을 정하는 단계**입니다.  
- **C4·C7·C8**만 정해도 TM/EFEM 이송 알고리즘과 Side Storage 연동 방향이 거의 확정됩니다.  
- 위 표 **결정** 칸을 채워 주시면 → `EquipmentCapacityConfig` + `ClusterScheduler` 초안 코드로 이어가면 됩니다.
