#!/usr/bin/env python3
"""JSONL 스냅샷을 학습용 CSV로 변환."""

from __future__ import annotations

import argparse
import csv
import json
from pathlib import Path


FIELDNAMES = [
    "timestamp_utc",
    "equipment_state",
    "alarm_code",
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
        default="bin/Debug/net8.0-windows/data/ai_training_snapshots.jsonl",
        help="Input JSONL path",
    )
    parser.add_argument(
        "--output",
        default="tools/ai/output/training_dataset.csv",
        help="Output CSV path",
    )
    return parser.parse_args()


def main() -> None:
    args = parse_args()
    input_path = Path(args.input)
    output_path = Path(args.output)
    output_path.parent.mkdir(parents=True, exist_ok=True)

    if not input_path.exists():
        raise FileNotFoundError(f"Input not found: {input_path}")

    rows = 0
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
            row = {
                "timestamp_utc": data.get("timestampUtc", ""),
                "equipment_state": data.get("equipmentState", ""),
                "alarm_code": alarm_code,
                "label_alarm": "1" if alarm_code else "0",
                "interlock_ok": int(bool(data.get("interlockOk", False))),
                "bench_mode": int(bool(data.get("benchMode", False))),
                "temperature": data.get("temperature", 0.0),
                "humidity": data.get("humidity", 0.0),
                "pressure": data.get("pressure", 0.0),
                "vibration": data.get("vibration", 0.0),
                "access_safe": int(bool(data.get("accessSafe", False))),
                "module_running_count": data.get("moduleRunningCount", 0),
                "module_alarm_count": data.get("moduleAlarmCount", 0),
                "module_processing_count": data.get("moduleProcessingCount", 0),
                "chamber_processing_count": data.get("chamberProcessingCount", 0),
            }
            writer.writerow(row)
            rows += 1

    print(f"done: {rows} rows -> {output_path}")


if __name__ == "__main__":
    main()
