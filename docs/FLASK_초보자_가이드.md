# etchflask 웹 대시보드 — 초보자 가이드

> **대상:** Flask를 처음 켜 보는 사람 · 모니터링 PC에서 브라우저만 쓰는 사람  
> **저장소:** [Flask-etchflask](https://github.com/yaong832/Flask-etchflask) (로컬 예: `C:\etchflask`)  
> **역할:** **보기·이력·AI 조언** — 공정 Start/Stop은 **WPF HMI**에서만 합니다.

---

## 1. Flask가 하는 일 / 하지 않는 일

| ✅ 하는 일 | ❌ 하지 않는 일 |
|-----------|----------------|
| WPF가 보낸 센서·모듈·레시피 **표시** | 장비 **제어** (Start/Stop) |
| 이력·이벤트 **저장·조회** (메모리 또는 SQLite) | 인터락 **해제·변경** |
| AI **이상 점수·예상 알람** 표시 (조언) | PLC 직접 연결 |

```
현장 PC:  WPF ──POST──► Flask ──► 브라우저(모니터링 PC)
                sensor-data      (GET만, 조회)
```

---

## 2. 실행 방법 (5분)

### 현장 PC

1. **FarmUI** 등 다른 5000 포트 프로그램이 떠 있으면 **종료**합니다.  
2. `C:\etchflask\run_flask.bat` 더블클릭 (또는 저장소에서 `python app.py`).  
3. 브라우저에서 `http://127.0.0.1:5000` 접속.  
4. **WPF HMI**를 실행하고 로그인 → **시뮬 허용** → **Start** (데모) 또는 실장비 연결.

WPF 헤더 **Flask** 칩이 **OK**이면 연동 정상입니다.

### 모니터링 PC (다른 PC)

1. 현장 PC IP 확인 (`ipconfig` → IPv4).  
2. 브라우저: `http://<현장PC IP>:5000`  
3. Windows 방화벽에서 **TCP 5000** 허용 필요.  
   상세: etchflask `REMOTE_MONITORING.md`

---

## 3. 화면 위쪽 — 탭 설명

브라우저에서 **가로 탭** 6개가 있습니다.

| 탭 | 누가 쓰나요? | 무엇이 보이나요? |
|----|-------------|------------------|
| **실시간** | 운전 중 모니터링 | 압력·진동·온습도 카드, Load Lock 요약, **추세 차트**, 하단 스냅샷 로그 |
| **서버 이력** | 추세·통계 | Flask 메모리에 쌓인 시계열, RUNNING 비율 등 KPI |
| **이벤트** | 알람·운전 이력 | Start/Stop, 알람, 인터락 관련 **이벤트 목록** |
| **모듈 상태** | 장비 엔지니어 | LP·BM·TM·PM별 상태 (WPF `modules[]` POST) |
| **레시피** | 공정 담당 | 활성 레시피 ID·PM 순서·tick (WPF POST `recipe`) |
| **AI 진단** | 품질·예지 보전 | 이상 점수, 예상 알람, 조치 문구 (**WPF와 문구 통일**) |

**팁:** 탭은 **2초마다** 자동 갱신됩니다(AI·모듈·레시피 탭은 해당 탭이 열려 있을 때).

---

## 4. 실시간 탭 — 숫자가 `—`일 때

| 표시 | 의미 |
|------|------|
| 카드에 **숫자** | WPF가 **실측(EtherCAT)** 또는 **데모** 데이터를 보냄 |
| **—** (대시) | WPF 미실행, 또는 아직 POST 없음, 또는 `sensorsLive=false` |

데모만 할 때:

- WPF **시뮬 허용** + **Start** 후 약 2초 지나면 `dataSource=demo` 로 값이 채워집니다.  
- 실시간 탭 차트는 **브라우저 버퍼**(최대 80점)입니다.

---

## 5. demo / live — 데이터가 두 갈래인 이유

WPF POST에 `dataSource`가 붙습니다.

| 값 | 의미 | 웹에서 보는 곳 |
|----|------|----------------|
| **live** | EtherCAT **실측** 가공 | `modules/latest?source=live` 등 |
| **demo** | **시뮬 허용** 데모 | `?source=demo` |
| **offline** | 하트비트만, 저장 생략 | 스냅샷 거의 없음 |

**발표·과제**는 보통 **demo** 로 쌓입니다. 실장비 검증은 **live**.

---

## 6. AI 진단 탭 (WPF와 맞춰 둔 화면)

### 6.1 상단 KPI

| 항목 | 설명 |
|------|------|
| **이상 점수** | 0~1에 가깝게 높을수록 이상 징후 (색: 녹·황·적) |
| **AI 엔진** | **ML** = sklearn 모델 로드, **규칙** = 스텁 폴백, **OFF** = 조회 실패 |
| **갱신** | 마지막 `ai/latest` 시각 |

점수 옆 `(ML)` / `(규칙)` 은 WPF 중앙 패널과 같은 의미입니다.

### 6.2 권고 박스

| 문구 | 설명 |
|------|------|
| **예상 알람** | `A001`~`A006` 코드 · 제목 · 신뢰도 · **조치 가이드** |
| **조치 문구** | Flask AI가 제안하는 운영자 행동 (자동 실행 안 함) |
| **ML 근거** | `topSignals` (모델 feature, ML일 때) |

예상 알람 코드는 WPF `AlarmCatalog`와 같은 계열입니다.

### 6.3 모델 상태 박스

`GET /api/etch/ai/status` JSON — `ready`, `engine`, `metrics` 등.  
개발·점검용이며, 일반 운영은 **이상 점수·예상 알람**만 봐도 됩니다.

### 6.4 AI가 안 나올 때

1. WPF **Flask OK** 인지 확인  
2. WPF **RUNNING** (또는 데모 Start) — sensor-data가 나가야 AI 갱신  
3. 5~10초 대기  
4. `models/etch/*.joblib` 없으면 **규칙** 스텁만 동작 (정상)  
   ML 쓰려면: etch_ui `tools/ai/train_from_sim.ps1 -Deploy` 후 Flask **재시작**

---

## 7. 모듈 상태 · 레시피 탭

### 모듈 상태

- WPF가 보낸 `modules[]` 의 최신 스냅샷.  
- **demo** / **live** 소스 선택(쿼리 또는 UI).  
- RUNNING·ALARM·PROCESSING 등 **모듈별 색·문구**.

### 레시피

- 활성 공정 레시피: PM 순서(PM2→PM3→PM4), Etch/Strip tick.  
- WPF **설정**·XML 레시피와 동기화됩니다.

---

## 8. 이벤트 · 서버 이력

| 탭 | 용도 |
|----|------|
| **이벤트** | 알람 발생, Start/Stop, 인터락 상실 등 **문장 로그** |
| **서버 이력** | 압력·진동 등 **시계열** (서버 재시작 시 메모리 모드는 초기화) |

영구 저장이 필요하면 Flask 실행 전 `set ETCH_USE_DB=1` 후 실행 → `data\etch_monitoring.db`

---

## 9. WPF와 함께 쓰는 추천 시나리오

### 발표 (1대 PC)

1. `run_flask.bat`  
2. WPF F5 → 시뮬 허용 → 데모 진행 또는 Start  
3. 브라우저 **실시간** + **AI 진단** 탭을 **두 번째 창**으로 띄움  
4. WPF **도식 확대**를 프로젝터에 (→ 외부 모니터) — [`HMI_초보자_가이드.md`](HMI_초보자_가이드.md)

### 운전 (2대 PC)

| PC | 화면 |
|----|------|
| 현장 | WPF 전체화면 조작 |
| 사무실/상황실 | 브라우저 `http://현장IP:5000` — **실시간**·**모듈**·**AI** |

---

## 10. 자동 검증 (개발자용)

```powershell
cd D:\WPFProject\etch_ui
.\tools\flask\e2e_flask.ps1 -RequireMl
```

성공 시 API·AI·모듈·레시피 계약이 맞는 상태입니다. 상세: [`FLASK_E2E.md`](FLASK_E2E.md)

---

## 11. 더 보기

| 문서 | 위치 |
|------|------|
| API·AI 학습 | etchflask `ETCH_AI.md` |
| 2PC·방화벽 | etchflask `REMOTE_MONITORING.md` |
| WPF 분리 창·외부 모니터 | [`HMI_초보자_가이드.md`](HMI_초보자_가이드.md) |
| 실행 순서 | etch_ui `PROTO_실행순서.md` |
