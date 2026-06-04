# FOUP 픽업·PM 레시피·Side Storage 정책 (합의)

> [`SCHEDULER_장비로직_설계.md`](SCHEDULER_장비로직_설계.md) 용량 표 보완.  
> 코드: `FoupPickScheduler`, `EquipmentCapacityConfig`, `TmTransferSimulator`.

---

## 1. FOUP / LP 픽업 (EFEM·TM 자율 판단 1차)

### 1.1 기본 규칙

| 순위 | 규칙 |
|------|------|
| A | **FOUP 안에 남은 매수가 가장 많은 LP**에서 픽업 (동시에 끝나게) |
| B | 남은 매수 **동일**하면 **LP1 → LP2 → LP3** |
| C | **신규 풀 FOUP 차단** — 아래 §1.2 |

### 1.2 풀 FOUP(25매) 끼어들기 방지 — **LP1·2·3 공통**

**문제 예:** LP1 마지막 1매 → 새 FOUP 25매 → 잔량 최대 규칙이 LP1만 다시 선택.  
같은 현상이 **LP2·LP3 재장착**에도 동일하게 발생.

**대응 (모든 LP에 동일):**

| 이 LP | 픽업 불가 조건 |
|-------|----------------|
| 잔량 **= 25매(풀)** | **다른 LP** 중 하나라도 `잔량 1~24` **또는** `장내 InFlight > 0` |

- 재장착 직후 LP1뿐 아니라, **손대지 않은 25매 FOUP(LP2/LP3)** 도 다른 LP Lot이 진행 중이면 **대기**
- **첫 투입**(3 FOUP 모두 25매·미개시)은 서로 “부분 Lot”이 없으므로 **LP1부터** 시작 (동률 LP1→2→3)
- **부분 Lot**(잔량 &lt; 25)이 하나라도 있으면, 풀 25매 LP는 **후순위** — 마지막 1매·장내 잔여를 먼저 소진

### 1.3 구현

- `Services/Scheduling/FoupPickScheduler.cs`
- 데모: LP1~3 각 **25매** 시작 (`EquipmentCapacityConfig`)

---

## 2. PM 식각(2~4) 배치 · Strip(PM1)

### 2.1 챔버 역할

| PM | 역할 | 시뮬 tick (기본) |
|----|------|------------------|
| **PM2~4** | 동일 **식각(Etch)** | `EtchProcessTicks = 75` (기본, `EquipmentCapacityConfig`) |
| **PM1** | **Strip만** (식각 완료 후 후공정) | `StripProcessTicks = 20` (기본) |

- PM당 수용 **1매** → 가공 중인 PM에는 **투입 불가** (별도 플래그 없이 물리 조건).
- 「PM1 가공 중 다른 PM 투입」이 아니라, **Strip은 PM1에서만** 한다는 뜻.

### 2.2 식각 PM 선택 (BM → PM2~4) — **합의**

**전부 비어 있을 때 (라인 기동·첫 투입):**

```text
PM2 → (없으면) PM3 → (없으면) PM4
```

고정 우선순위 **2 → 3 → 4**. (semitest 병렬 모드의 「둘 다 비면 B」와 동일 계열.)

**가동 중 (일부 PM 식각 중):**

1. **비어 있는 식각 PM** 중 위 순서로 배치 (**비어 있는 쪽 우선**).
2. PM2·3만 쓰는 중이고 PM4만 비면 → **PM4**.
3. 셋 다 가득 → BM/EFEM 쪽 **대기** (또는 Side Stg·다음 FOUP 등 다른 Job).

**한 매의 식각 경로 (레시피):**

- 기본: PM2 → PM3 → PM4 **순차 스텝** (직렬 경로).
- 병렬 가동: **여러 매**가 PM2/3/4 **각각 다른 챔버**에서 **동시에** Etch tick 진행 가능 (TM이 1매면 이송은 번갈아).

### 2.3 PM1 (Strip)

- **식각(PM2~4) 완료 후** BM → PM1만 허용.
- PM1 `Processing` 중 PM1 추가 투입 **불가** (1매 수용).

### 2.4 구현 (`EfemTransferScheduler` / `VacuumTransferScheduler` + `TmTransferSimulator`)

| 합의 | 코드 |
|------|------|
| Etch 파이프라인 **2→3→4** 슬롯 채움 | `EtchPmSelector.SelectNextPipelineTarget` |
| BM → Etch PM 투입 | `VacuumTransferScheduler.TryScheduleBmToEtchPm` |
| Etch 완료 → PM1 Strip (BM 경유 없음) | `TryScheduleEtchPmToPm1Strip` |
| 다매·PM 동시 `RemainingProcessTicks` | `ClusterEquipmentState.DecrementProcessTimes` |
| 이송 Job 큐 | `TransferJob` + 각 스케줄러 `TryScheduleOne` |

### 2.5 TM 2매(dual blade) (C4 — 시뮬·UI 구현)

FOUP 3×25매와 궁합이 좋음. **회전 블레이드 양끝 2슬롯** 가정.

| 항목 | TM 1매 (현재) | TM 2매 |
|------|---------------|--------|
| 동시 이송 Job | 1 active | 최대 **2** (슬롯 A/B) |
| 스케줄러 | FIFO 1큐 | **2-token** + 충돌 없는 (pick,drop) 쌍 |
| 처리량 | 낮음 | PM2~4 병렬 식각과 맞물려 **↑** |
| 구현 | `TmTransferSimulator`, `VacuumDualBladePlanner`, `RobotBladeSlots` | Etch→PM1 연속 픽업·슬롯 A/B |
| 도식 | `EquipmentSchematicControl` | TM 중심 **앞(+)/뒤(-)** 180° 대칭 팔 · `TmRotate`로 챔버 맞춤 (슬롯 A=뒤, B=앞) |

스케줄이 **많이 달라짐**: 같은 틱에 「PM3 pick + PM4 place」 등 **비겹치는** 조합 가능, BM·동일 PM 슬릿 **동시 접근 금지**.

---

## 3. Side Storage · 출하 (다음 공정 FOUP)

| 항목 | 값 |
|------|-----|
| 용량 | **25매** (`SideStorageSlotCount`) |
| 이송 | Strip 완료 웨이퍼: **BM → Side Stg** (`EfemTransferScheduler`) |
| 만석 | 25매 시 **BM→Side Stg HOLD** · `PerformSideStorageCassetteSwap()`로 **25매 일괄 출하** |
| LOT | 출하 1매당 `Lot.RecordWaferCompleted()` — 목표 **75매** (3 FOUP × 25) |

**다음 공정 FOUP (`NextProcessFoupA/B/C`):** 도식·레거시 영역만. **별도 장비·이송 Job 없음** — 출하는 Side Stg **카세트 교체(가상)** 로 처리. UI·스케줄 확장은 **보류**.

- `ExternalProcess` / `NextProcessFoup*` — HMI 표시·호환용; 실 FOUP 재장착 시나리오는 미구현.

---

## 4. 구현·보류 (코드 기준)

- [x] EFEM/진공 분리 스케줄러 + **§2.2** 파이프라인·공정 tick
- [x] TM 듀얼 블레이드 (시뮬 `VacuumDualBladePlanner`, 도식 슬롯 A/B)
- [x] Side Stg 25 + 카세트 교체 출하 + LOT COMPLETE (`LotCompletionTracker`)
- [ ] 레시피 XML/JSON · Flask 레시피 연동 — **후순위**
- [ ] AI 조언·학습 파이프라인 UI — **후순위** (`PROJECT_모듈상태_AI_계획.md`)
- [ ] 다음 공정 FOUP 물리 이송 — **범위 밖(가상 출하로 대체)**

---

## 5. 헤드리스 시뮬 (`--sim-smoke` / `--sim-report`)

| 명령 | 용도 |
|------|------|
| `dotnet run -- --sim-smoke [--ticks=N]` | 짧은 tick·상태 불변식·크래시 없음 |
| `dotnet run -- --sim-policy-batch [--runs=N] [--ticks=N]` | FOUP 정책·파이프라인·BM 수용 배치 |
| `dotnet run -- --sim-report [--ticks=N]` | KPI·잔량·`lot=x/75` 스냅샷 (기본 tick 40000) |
| `dotnet run -- --sim-dual-blade [--ticks=N]` | 2슬롯·연속픽업·180°회전·A/B 사용 검증 |

**LOT 75/75:** Side Stg **카세트 3회**(25×3) 출하 후 `IsLotComplete()`. tick이 부족하면 `lot_done=False`로 끝날 수 있음.

**BM 만석 HOLD·진공 가동률 99%:** 시뮬에서 **일시적**으로 나올 수 있는 스케줄 메시지. 실장비에서는 EFEM이 BM을 비우며 **상시 정체**로 가는 구조가 아님(§5.1). 영구 `lot=0`은 **장시간 헤드리스 튜닝·병목 검증** 이슈이지, 현장 정상 운전 예상 상태가 아님.

### 5.1 헤드리스 vs 현장 (병목)

| 구분 | 헤드리스 시뮬 | 현장·UI 데모 |
|------|----------------|--------------|
| 목적 | 회귀·정책·KPI 스냅샷 | 운전자가 보는 가동·도식 |
| tick | Etch 75 / Strip 20 등 **튜닝값** | 동일 설정, **시간 압축** |
| 병목 | BM·TM 스케줄 경합으로 **처리량 저하** 가능 | EFEM·진공 **병렬** 가동, Side 출하로 LOT 진행 |
| 판단 | `lot`·`kpi`로 **추세** 확인 | RUNNING·잔량·LOT COMPLETE **눈으로 확인** |

듀얼 블레이드·도식은 **UI RUNNING**에서 확인. 정책·안정성은 **§5 명령**으로 확인.
