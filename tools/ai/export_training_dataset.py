#!/usr/bin/env python3
"""WPF 시뮬 JSONL → 학습용 CSV (alarm_class·label_alarm 자동 도출)."""

from __future__ import annotations

import argparse
import csv
import json
import sys
from pathlib import Path

_TOOLS_AI = Path(__file__).resolve().parent
sys.path.insert(0, str(_TOOLS_AI))
from alarm_labels import derive_alarm_class, label_is_anomaly  # noqa: E402

FIELDNAMES = [
    "timestamp_utc",
    "equipment_state",
    "alarm_code",
    "alarm_class",
    "label_alarm",
    "interlock_ok",
    "bench_mode",
    "temperature",
    "humidity",
    "pressure",
    "vibration",
    "access_safe",
    "module_running_count",
    "module_alarm_count",
    "module_processing_count",
    "chamber_processing_count",
]


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Export JSONL snapshots to CSV dataset")
    parser.add_argument(
        "--input",
        default="",
        help="Input JSONL (비우면 bin/Debug·Release·data 순 탐색)",
    )
    parser.add_argument(
        "--output",
        default="tools/ai/output/training_dataset.csv",
        help="Output CSV path",
    )
    parser.add_argument("--min-rows", type=int, default=0, help="미만이면 경고 (0=무시)")
    return parser.parse_args()


def resolve_jsonl(explicit: str) -> Path:
    if explicit:
        p = Path(explicit)
        if not p.is_file():
            raise FileNotFoundError(f"Input not found: {p}")
        return p

    repo = Path(__file__).resolve().parents[2]
    candidates = [
        repo / "bin/Debug/net8.0-windows/data/ai_training_snapshots.jsonl",
        repo / "bin/Release/net8.0-windows/data/ai_training_snapshots.jsonl",
        repo / "data/ai_training_snapshots.jsonl",
    ]
    for c in candidates:
        if c.is_file():
            return c
    raise FileNotFoundError(
        "JSONL 없음 — WPF 실행 후 「시뮬 허용」+ 공정 Start로 5분 이상 수집하세요.\n"
        "경로 예: bin/Debug/net8.0-windows/data/ai_training_snapshots.jsonl"
    )


def main() -> None:
    args = parse_args()
    input_path = resolve_jsonl(args.input)
    output_path = Path(args.output)
    output_path.parent.mkdir(parents=True, exist_ok=True)

    rows = 0
    alarm_rows = 0
    with input_path.open("r", encoding="utf-8") as src, output_path.open(
        "w", encoding="utf-8", newline=""
    ) as dst:
        writer = csv.DictWriter(dst, fieldnames=FIELDNAMES)
        writer.writeheader()
        for line in src:
            line = line.strip()
            if not line:
                continue
            data = json.loads(line)
            alarm_code = (data.get("alarmCode") or "").strip()
            equipment_state = data.get("equipmentState", "")
            pressure = float(data.get("pressure", 0.0))
            vibration = float(data.get("vibration", 0.0))
            temperature = float(data.get("temperature", 0.0))
            humidity = float(data.get("humidity", 0.0))
            interlock_ok = bool(data.get("interlockOk", False))
            access_safe = bool(data.get("accessSafe", False))

            alarm_class = derive_alarm_class(
                alarm_code=alarm_code,
                equipment_state=equipment_state,
                pressure=pressure,
                vibration=vibration,
                interlock_ok=interlock_ok,
                access_safe=access_safe,
                temperature=temperature,
                humidity=humidity,
            )
            label_alarm = label_is_anomaly(alarm_class, alarm_code, equipment_state)

            row = {
                "timestamp_utc": data.get("timestampUtc", ""),
                "equipment_state": equipment_state,
                "alarm_code": alarm_code,
                "alarm_class": alarm_class,
                "label_alarm": label_alarm,
                "interlock_ok": int(interlock_ok),
                "bench_mode": int(bool(data.get("benchMode", False))),
                "temperature": temperature,
                "humidity": humidity,
                "pressure": pressure,
                "vibration": vibration,
                "access_safe": int(access_safe),
                "module_running_count": int(data.get("moduleRunningCount", 0)),
                "module_alarm_count": int(data.get("moduleAlarmCount", 0)),
                "module_processing_count": int(data.get("moduleProcessingCount", 0)),
                "chamber_processing_count": int(data.get("chamberProcessingCount", 0)),
            }
            writer.writerow(row)
            rows += 1
            if label_alarm:
                alarm_rows += 1

    print(f"source: {input_path}")
    print(f"done: {rows} rows ({alarm_rows} anomaly) -> {output_path}")

    if args.min_rows and rows < args.min_rows:
        print(
            f"WARN: {rows} < {args.min_rows} — 시뮬 RUNNING을 더 길게 돌리거나 「데모 진행」 후 수동 Start 유지",
            file=sys.stderr,
        )
        sys.exit(2)


if __name__ == "__main__":
    main()
