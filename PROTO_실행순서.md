# 식각 HMI 프로토타입 실행 순서

## 관련 문서

| 문서 | 경로 |
|------|------|
| **구현 상태 체크리스트** | `d:\wpf과제프로젝트\구현_상태_체크리스트.md` |
| **PLC/EtherCAT I/O 매핑** | `PLC_IO_매핑.md` (도어 = 유도형 DI 비트5) |
| **원격 모니터링 (Flask)** | `C:\etchflask\REMOTE_MONITORING.md` |
| **Flask README** | `C:\etchflask\README.md` |
| **발표 PPT** | `d:\wpf과제프로젝트\플라즈마_식각_장비_HMI_PPT_최종본 (1).pptx` |

## 2대 PC 구성 (권장)

| PC | 할 일 |
|----|--------|
| **현장 PC** | EtherCAT(I/O) + WPF + `run_flask.bat` (Flask 서버) |
| **모니터링 PC** | 브라우저만 → `http://<현장PC IP>:5000` |

자세한 내용: `C:\etchflask\REMOTE_MONITORING.md`

## 1) Flask — **현장 PC**에서 실행

1. `C:\etchflask\run_flask.bat` 더블클릭  
2. 현장 PC 브라우저: `http://127.0.0.1:5000`  
3. 모니터링 PC: `http://192.168.x.x:5000` (현장 PC `ipconfig` IPv4)  
4. 방화벽: TCP **5000** 인바운드 허용  

API: `GET /api/sensors` · `GET /api/etch/history` · `GET /api/etch/events` · `GET /api/etch/summary`

## 2) WPF HMI — **현장 PC**

1. `D:\WPFProject\etch_ui\etch_ui.sln` → F5  
2. 로그인: `admin` / `Admin1234`  
3. **EtherCAT: Connected** 후에만 화면에 센서 수치 표시 (미연결·시뮬은 **—**)  
4. `appsettings.json`의 `FlaskBaseUrl`은 현장 PC 기준 **`http://127.0.0.1:5000`** (모니터링 PC IP 아님)

## 설정

- **`FlaskBaseUrl`**: WPF → 같은 PC의 Flask POST 주소 (현장 PC = `127.0.0.1`)  
- **`AdsPort`**, **`SimulationEnabled`**, **`Interlock`**: `appsettings.json` 참고  
- 시뮬 허용 ON이어도 **화면·Flask 웹**에는 EtherCAT 실측 전 센서 숫자 미표시

## 연동 데이터

- WPF → `POST /api/etch/sensor-data` (약 2초마다, 센서 이름 `압력`·`진동` 등)
- 웹/대시보드 → `GET /api/sensors` (최신 스냅샷 + `equipmentState`, `alarmCode` 등)
