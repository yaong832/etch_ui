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
| **PM2~4** | 동일 **식각(Etch)** | `EtchProcessTicks = 30` |
| **PM1** | **Strip만** (식각 완료 후 후공정) | `StripProcessTicks = 12` |

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

### 2.4 구현 (`ClusterScheduler` + `TmTransferSimulator`)

| 합의 | 코드 |
|------|------|
| 매별 식각 **2→3→4** 순차 | `EtchPmSelector.GetEtchTargetForStep` + `EtchStepIndex` |
| BM 투입 시 해당 step PM **비어 있을 때만** | `ResolveBmEtchTarget` |
| 다매·PM 동시 `RemainingProcessTicks` | `ClusterEquipmentState.DecrementProcessTimes` |
| TM 1매 Job 큐 | `TransferJob` + `TryScheduleOne` |

### 2.5 TM 2매(dual blade) 가정 시 (C4 — 미구현, 방향만)

FOUP 3×25매와 궁합이 좋음. **회전 블레이드 양끝 2슬롯** 가정.

| 항목 | TM 1매 (현재) | TM 2매 |
|------|---------------|--------|
| 동시 이송 Job | 1 active | 최대 **2** (슬롯 A/B) |
| 스케줄러 | FIFO 1큐 | **2-token** + 충돌 없는 (pick,drop) 쌍 |
| 처리량 | 낮음 | PM2~4 병렬 식각과 맞물려 **↑** |
| 구현 | `TmTransferSimulator` | Phase 2: `BladeSlot`, `TransferPlanner` 충돌 매트릭스 |

스케줄이 **많이 달라짐**: 같은 틱에 「PM3 pick + PM4 place」 등 **비겹치는** 조합 가능, BM·동일 PM 슬릿 **동시 접근 금지**.

---

## 3. Side Storage · 다음 공정 FOUP

| 항목 | 값 |
|------|-----|
| 용량 | **25매** (`SideStorageSlotCount`) |
| 연결 | LP별 **다음 공정 FOUP** (`NextProcessFoupA/B/C`) — Side Stg와 **1:1 직결** |
| 이송 | BM → Side Stg 적치 → **FIFO**로 해당 LP의 **다음 공정 FOUP**으로 즉시 이송 Job |
| 만석 | 25매 시 **신규 BM→Side Stg HOLD** |

- 구버전 `ExternalProcess` 단일 포트는 **레거시**; 신규는 `NextProcessFoup*`.

---

## 4. 미구현 (다음 단계)

- [x] `ClusterScheduler` + **§2.2** (매별 2→3→4, 다매 공정 tick)
- [ ] 레시피 XML / 스텝 부분 집합
- [ ] TM 2매 dual blade (선택, C4)
- [ ] 레시피 JSON / Flask 연동
- [ ] 신규 FOUP 장착 UI·Lot ID 이벤트
