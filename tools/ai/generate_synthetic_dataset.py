#!/usr/bin/env python3
"""WPF 수집 전·CI용 합성 학습 CSV 생성."""

from __future__ import annotations

import argparse
import csv
import random
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

STATES = ["IDLE", "READY", "RUNNING", "WARNING", "ALARM", "MAINTENANCE"]
ALARM_CODES = ["A001", "A002", "A003", "A004", "A005", "A006"]


def parse_args() -> argparse.Namespace:
    p = argparse.ArgumentParser(description="Generate synthetic etch training CSV")
    p.add_argument("-n", "--rows", type=int, default=3000, help="Number of rows")
    p.add_argument(
        "-o",
        "--output",
        default="tools/ai/output/training_dataset.csv",
        help="Output CSV path",
    )
    p.add_argument("--seed", type=int, default=42)
    return p.parse_args()


def normal_sample(rng: random.Random, state: str) -> dict:
    return {
        "equipment_state": state,
        "alarm_code": "",
        "label_alarm": 0,
        "interlock_ok": 1,
        "bench_mode": int(state in ("RUNNING", "READY") and rng.random() < 0.7),
        "temperature": rng.uniform(22, 28),
        "humidity": rng.uniform(35, 50),
        "pressure": rng.uniform(80, 130),
        "vibration": rng.uniform(0.05, 0.45),
        "access_safe": 1,
        "module_running_count": rng.randint(1, 6) if state == "RUNNING" else 0,
        "module_alarm_count": 0,
        "module_processing_count": rng.randint(0, 3) if state == "RUNNING" else 0,
        "chamber_processing_count": rng.randint(0, 2) if state == "RUNNING" else 0,
    }


def alarm_sample(rng: random.Random, code: str) -> dict:
    row = normal_sample(rng, "ALARM")
    row["alarm_code"] = code
    row["label_alarm"] = 1
    row["equipment_state"] = "ALARM"
    if code == "A002":
        row["pressure"] = rng.choice([rng.uniform(20, 45), rng.uniform(160, 220)])
    elif code == "A003":
        row["vibration"] = rng.uniform(0.85, 1.4)
    elif code == "A004":
        row["access_safe"] = 0
        row["interlock_ok"] = 0
    elif code == "A005":
        row["temperature"] = rng.choice([rng.uniform(15, 18), rng.uniform(32, 38)])
    elif code == "A006":
        row["humidity"] = rng.choice([rng.uniform(20, 28), rng.uniform(58, 72)])
    elif code == "A001":
        row["bench_mode"] = 0
        row["interlock_ok"] = 0
    row["module_alarm_count"] = rng.randint(1, 3)
    return row


def main() -> None:
    args = parse_args()
    rng = random.Random(args.seed)
    out = Path(args.output)
    out.parent.mkdir(parents=True, exist_ok=True)

    rows: list[dict] = []
    n_alarm = int(args.rows * 0.22)
    n_normal = args.rows - n_alarm

    for _ in range(n_normal):
        st = rng.choices(
            ["IDLE", "READY", "RUNNING", "WARNING", "MAINTENANCE"],
            weights=[15, 20, 45, 12, 8],
        )[0]
        rows.append(normal_sample(rng, st))

    for _ in range(n_alarm):
        code = rng.choice(ALARM_CODES)
        rows.append(alarm_sample(rng, code))

    rng.shuffle(rows)

    with out.open("w", encoding="utf-8", newline="") as f:
        w = csv.DictWriter(f, fieldnames=FIELDNAMES)
        w.writeheader()
        for i, row in enumerate(rows):
            row["timestamp_utc"] = f"2026-01-01T00:{i % 60:02d}:{i % 60:02d}Z"
            w.writerow(row)

    alarm_n = sum(1 for r in rows if r["label_alarm"])
    print(f"done: {len(rows)} rows ({alarm_n} alarm) -> {out}")


if __name__ == "__main__":
    main()
