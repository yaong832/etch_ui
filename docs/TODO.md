# etch_ui 작업 목록 (Flask 서버 제외)

> **전제:** 이 저장소는 **C# WPF HMI**만 포함합니다. Flask(`etchflask`)는 별도 저장소에서 나중에 연동합니다.  
> Flask **서버 코드·API 스펙 변경**은 `etchflask` 업로드 후 진행합니다.

---

## A. 바로 착수 가능 (Flask 불필요)

### 1. 빌드·코드 품질

- [ ] `MainWindow.xaml.cs` 빌드 경고 수정 (CS4014 async, CS8600 nullable)
- [ ] `.editorconfig` / nullable 정리
- [ ] TwinCAT 없을 때 **시뮬만으로 실행**하는 방법을 `README.md`에 명시

### 2. 문서·포트폴리오

- [ ] `PROTO_실행순서.md`의 `D:\`, `C:\etchflask` 절대경로 → 「Flask는 별도 저장소」로 정리
- [ ] README에 스크린샷·데모 시나리오 추가 (로그인 → 시뮬 허용 → 공정 Start)
- [ ] 구현 상태 체크리스트를 `docs/`에 유지·갱신

### 3. UI/UX (WPF만)

- [ ] `MainWindow.xaml` 레이아웃·폰트·색·테마 정리
- [ ] 센서 카드, 인터락 패널, 로그 영역 가독성 개선
- [ ] `LoginWindow` / `UserManagementWindow` 문구·검증 메시지
- [ ] 고해상도·창 크기별 `EquipmentSchematicControl` 스케일 점검

### 4. 장비 도식 (1단계 확장, `Equipment/`)

- [ ] 도어/램프/웨이퍼 표시 다듬기
- [ ] 시뮬 TM 경로·속도·단계 설명 UI
- [ ] `WPF_장비UI_이식_계획.md` 중 WPF만 가능한 항목 (예: Storyboard 도어)
- [ ] FOUP 슬롯 표시 (DataTemplate) — PLC/Flask 무관

### 5. 보안·계정 (`Security/`)

- [x] 관리자 전용 계정 등록 (공개 가입 없음)
- [x] 본인 비밀번호 변경 · 관리자 비밀번호 재설정
- [x] 이벤트·알람 DB 조회 UI
- [ ] `PasswordPolicy` 강화 (특수문자·만료 등)
- [ ] 역할별 버튼 숨김 (관리자만 사용자 관리 등) 점검

### 6. 인터락·알람·상태기

- [x] `appsettings.json` 인터락·압력 스케일 — 관리자 **설정** 창 (`InterlockSettingsWindow`)
- [x] 가상 시뮬 **레시피** — XML (`Recipes/default.process.xml`) · PM 순서 · tick · Flask `recipe` POST
- [ ] `AlarmCatalog` A001~A006 문구·조치 가이드 보강
- [x] `ProcessStepLadderControl` ↔ `SimPhase`·PhaseHint 동기화
- [x] FOUP LP1~3 잔량 ProgressBar · PM (가상) / Load Lock (실접촉) 라벨
- [x] 유지보수 모드 — 진입 시 이송 정지·인터락 완화 표시·Flask `MaintenanceMode`

### 7. 시뮬레이션 모드 (PLC 없이 데모)

- [x] 「시뮬 허용」 + **데모 가이드** 창 (3분 시나리오)
- [ ] 시뮬 센서 값·상태 전환 자동 시나리오 버튼
- [ ] EtherCAT 미연결 시 화면 메시지·가이드 통일

### 8. 아키텍처 리팩터링

- [ ] `MainWindow.xaml.cs` 분리 (예시)
  - [ ] `PlcPollingService`
  - [ ] `InterlockEvaluator`
  - [ ] `EquipmentStateMachine`
  - [ ] `HmiTelemetryPublisher` (Flask POST 호출만 모음)
- [ ] ViewModel `ICommand` 이동, 코드비하인드 축소

### 9. PLC / TwinCAT (`Plc/`) — 장비·TwinCAT 있을 때

- [ ] `PlcAdsService` 심볼·비트 매핑 (`PLC_IO_매핑.md`와 일치)
- [ ] ADS 재연결·타임아웃 튜닝
- [x] 압력 스케일링 (`PlcAnalogScaling`, `AppSettings` / 설정 UI)
- [ ] 램프 DO·버튼 DI 엣지 처리

### 10. Flask 클라이언트 (`Services/EtchFlaskClient.cs`)

- [x] `MaintenanceMode` · 모듈 배열 POST
- [x] `POST /api/etch/events` — WPF 이벤트 전달 (Phase 3.6)
- [x] 로컬 `telemetry_samples` (Flask OFF 시에도 샘플 보존)
- [x] POST 실패 로그 스로틀 · Flask OFF 표시
- [x] `GET /api/etch/modules/latest` (Flask)
- [x] Flask 웹 **모듈 상태** 탭 (테이블·미니 그리드)
- [x] 설정 저장 확인 + **관리자 비밀번호 재확인** + Flask `settings_change` 이벤트

---

## B. Flask 실행만 하면 테스트 가능 (서버 수정 불필요)

> `etchflask`를 로컬에서 실행한 뒤 검증. API는 **현재 문서에 있는 엔드포인트** 기준.

- [ ] 상단 Flask 연결 상태 표시 (연결됨/끊김)
- [ ] `POST /api/etch/sensor-data` 전송 주기·payload 검증
- [ ] 브라우저 `GET /api/sensors`와 HMI 수치 일치 확인

---

## C. 보류 (Flask 저장소 또는 2단계 HW 필요)

| 항목 | 이유 |
|------|------|
| 웹 대시보드 UI·새 API | `etchflask` 저장소 |
| API 경로/JSON 필드 변경 | Flask + WPF 양쪽 |
| IEG3268 서보 실좌표 연동 | DLL·하드웨어, 2단계 (`EquipmentMotionBridge` TODO) |
| Transfer 시퀀스 전체 이식 | WinForms 원본 + 공수 |
| 원격 2PC 방화벽·배포 스크립트 | Flask + 인프라 |

---

## D. 추천 우선순위

1. README + 문서 경로 정리
2. 빌드 경고 제거
3. 시뮬 데모 시나리오 고정 (발표/포트폴리오)
4. `MainWindow` 서비스 분리 (1차)
5. 장비 도식·UI polish
6. (선택) TwinCAT 실연결 검증
7. **`etchflask` 업로드 후** end-to-end 연동 테스트

---

## E. 빠른 체크리스트

```
[ ] README: Flask 별도 repo, 시뮬만 실행 방법
[ ] PROTO 문서: 절대경로 제거
[ ] MainWindow 빌드 경고 2건
[ ] 시뮬 허용 + 데모 시나리오 문서화
[ ] UI / 장비 도식 polish
[ ] AlarmCatalog / 인터락 문구
[ ] MainWindow → 서비스 클래스 분리 (1차)
[ ] EtchFlaskClient 오프라인 UX
[ ] (선택) TwinCAT 실연결
[ ] (etchflask 후) E2E 연동 테스트
```

---

*마지막 갱신: 문서 추가 시 커밋 메시지 또는 PR에서 날짜를 갱신하세요.*
