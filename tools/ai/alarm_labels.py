"""시뮬·현장 스냅샷에서 학습용 알람 클래스·이상 라벨 도출 (Flask 스텁 규칙과 동일 계열)."""

from __future__ import annotations

ALARM_CLASSES = ["NONE", "A001", "A002", "A003", "A004", "A005", "A006"]

PRESSURE_LO = 50.0
PRESSURE_HI = 150.0
VIBRATION_MAX = 0.8


def derive_alarm_class(
    *,
    alarm_code: str = "",
    equipment_state: str = "",
    pressure: float = 0.0,
    vibration: float = 0.0,
    interlock_ok: bool = True,
    access_safe: bool = True,
    temperature: float = 25.0,
    humidity: float = 45.0,
    pressure_lo: float = PRESSURE_LO,
    pressure_hi: float = PRESSURE_HI,
    vibration_max: float = VIBRATION_MAX,
) -> str:
    code = (alarm_code or "").strip().upper()
    if code in ALARM_CLASSES and code != "NONE":
        return code

    st = (equipment_state or "").strip().upper()
    if st == "ALARM":
        return "A001"
    if not interlock_ok or not access_safe:
        return "A004"
    if pressure < pressure_lo or pressure > pressure_hi:
        return "A002"
    if vibration > vibration_max:
        return "A003"
    if st == "WARNING":
        return "A005"
    if temperature < 18.0 or temperature > 32.0:
        return "A005"
    if humidity < 28.0 or humidity > 58.0:
        return "A006"
    return "NONE"


def label_is_anomaly(alarm_class: str, alarm_code: str = "", equipment_state: str = "") -> int:
    if (alarm_code or "").strip():
        return 1
    if alarm_class and alarm_class != "NONE":
        return 1
    if (equipment_state or "").upper() in ("ALARM", "WARNING"):
        return 1
    return 0
