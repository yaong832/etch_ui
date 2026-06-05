# Flask(etchflask)와 FarmUI 분리

이 **etch_ui** 저장소에는 Flask 서버 코드가 없습니다. HTTP 클라이언트만 포함합니다.

| 프로그램 | 저장소·경로 | 포트 |
|----------|-------------|------|
| **WPF HMI** | `yaong832/etch_ui` · `D:\WPFProject\etch_ui` | — |
| **Etch Flask** | `yaong832/farmui` *(rename → etchflask 권장)* · `C:\etchflask` | 5000 |
| **FarmUI** | `C:\farmui\farmui` (스마트팜, 별도) | 5000 (동시 실행 불가) |

## 실행

1. 식각 데모: `C:\etchflask\run_flask.bat` 만 실행
2. WPF `appsettings.json` → `"FlaskBaseUrl": "http://127.0.0.1:5000"`
3. FarmUI가 필요하면 **etchflask를 끄고** `C:\farmui\farmui` 쪽 배치 실행

상세: `C:\etchflask\FARMUI_분리안내.md`
