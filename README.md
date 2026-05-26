# etch_ui

에칭 장비 HMI용 WPF(.NET 8) 클라이언트입니다. Flask 백엔드(`etchflask`)와 TwinCAT ADS PLC와 연동합니다.

## 요구 사항

- .NET 8 SDK
- Visual Studio 2022 (WPF)
- (선택) Beckhoff TwinCAT / ADS
- (선택) `etchflask` Flask 서버 — `appsettings.json`의 `FlaskBaseUrl` (기본 `http://127.0.0.1:5000`)

## 실행

1. `etch_ui.sln` 열기 → F5
2. 또는 `dotnet build` 후 `bin\Debug\net8.0-windows\etch_ui.exe`

## 문서

- `PROTO_실행순서.md` — Flask / WPF 실행 순서
- `PLC_IO_매핑.md` — PLC I/O 매핑
- `WPF_장비UI_이식_계획.md` — UI 이식 계획

## 설정

`appsettings.json`에서 Flask URL, ADS 포트, 시뮬레이션 여부 등을 수정합니다.
