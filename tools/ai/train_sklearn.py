#!/usr/bin/env python3
"""scikit-learn 이상·알람 코드 분류기 학습 → joblib + manifest (Flask models/etch)."""

from __future__ import annotations

import argparse
import json
from datetime import datetime, timezone
from pathlib import Path

import joblib
import numpy as np
from sklearn.ensemble import RandomForestClassifier
from sklearn.metrics import accuracy_score, classification_report
from sklearn.model_selection import train_test_split

import csv


def load_rows(path: Path) -> list[dict]:
    with path.open("r", encoding="utf-8", newline="") as f:
        reader = csv.DictReader(f)
        rows = []
        for row in reader:
            row["label_alarm"] = int(row["label_alarm"])
            row["pressure"] = float(row["pressure"])
            row["vibration"] = float(row["vibration"])
            row["temperature"] = float(row["temperature"])
            row["humidity"] = float(row["humidity"])
            row["module_alarm_count"] = int(row["module_alarm_count"])
            row["interlock_ok"] = int(row["interlock_ok"])
            row["access_safe"] = int(row["access_safe"])
            row["bench_mode"] = int(row["bench_mode"])
            row["module_running_count"] = int(row["module_running_count"])
            row["module_processing_count"] = int(row["module_processing_count"])
            row["chamber_processing_count"] = int(row["chamber_processing_count"])
            rows.append(row)
        return rows

FEATURE_NAMES = [
    "temperature",
    "humidity",
    "pressure",
    "vibration",
    "interlock_ok",
    "access_safe",
    "bench_mode",
    "module_running_count",
    "module_alarm_count",
    "module_processing_count",
    "chamber_processing_count",
    "state_idle",
    "state_ready",
    "state_running",
    "state_warning",
    "state_alarm",
    "state_maint",
]

ALARM_CLASSES = ["NONE", "A001", "A002", "A003", "A004", "A005", "A006"]


def parse_args() -> argparse.Namespace:
    p = argparse.ArgumentParser(description="Train sklearn models for etch Flask AI")
    p.add_argument("--dataset", default="tools/ai/output/training_dataset.csv")
    p.add_argument("--out-dir", default="tools/ai/output/models")
    p.add_argument("--test-size", type=float, default=0.2)
    p.add_argument("--seed", type=int, default=42)
    p.add_argument("--min-rows", type=int, default=80, help="최소 행 수 (시뮬 수집 기본 80)")
    p.add_argument(
        "--archive",
        action="store_true",
        help="학습 전 out-dir 기존 joblib을 output/models/archive/ 로 백업",
    )
    return p.parse_args()


def row_to_features(row: dict) -> list[float]:
    st = (row.get("equipment_state") or "IDLE").upper()
    flags = {s: 0.0 for s in ["IDLE", "READY", "RUNNING", "WARNING", "ALARM", "MAINTENANCE"]}
    if st in flags:
        flags[st] = 1.0
    return [
        float(row["temperature"]),
        float(row["humidity"]),
        float(row["pressure"]),
        float(row["vibration"]),
        float(row["interlock_ok"]),
        float(row["access_safe"]),
        float(row["bench_mode"]),
        float(row["module_running_count"]),
        float(row["module_alarm_count"]),
        float(row["module_processing_count"]),
        float(row["chamber_processing_count"]),
        flags["IDLE"],
        flags["READY"],
        flags["RUNNING"],
        flags["WARNING"],
        flags["ALARM"],
        flags["MAINTENANCE"],
    ]


def row_to_alarm_label(row: dict) -> str:
    cls = (row.get("alarm_class") or "").strip().upper()
    if cls in ALARM_CLASSES:
        return cls
    code = (row.get("alarm_code") or "").strip().upper()
    if code in ALARM_CLASSES and code != "NONE":
        return code
    return "NONE"


def build_xy(rows: list[dict]) -> tuple[np.ndarray, np.ndarray, np.ndarray]:
    x = np.array([row_to_features(r) for r in rows], dtype=np.float64)
    y_anomaly = np.array([int(r["label_alarm"]) for r in rows], dtype=np.int32)
    y_alarm = np.array([row_to_alarm_label(r) for r in rows], dtype=object)
    return x, y_anomaly, y_alarm


def main() -> None:
    args = parse_args()
    dataset = Path(args.dataset)
    out_dir = Path(args.out_dir)
    out_dir.mkdir(parents=True, exist_ok=True)

    if not dataset.exists():
        raise FileNotFoundError(
            f"Dataset not found: {dataset}. "
            "시뮬: .\\tools\\ai\\train_from_sim.ps1 또는 export_training_dataset.py"
        )

    rows = load_rows(dataset)
    if len(rows) < args.min_rows:
        raise RuntimeError(f"Need at least {args.min_rows} rows (got {len(rows)})")

    out_dir = Path(args.out_dir)
    if args.archive and (out_dir / "anomaly_classifier.joblib").is_file():
        stamp = datetime.now(timezone.utc).strftime("%Y%m%d-%H%M%S")
        archive_dir = out_dir / "archive" / stamp
        archive_dir.mkdir(parents=True, exist_ok=True)
        for name in ("anomaly_classifier.joblib", "alarm_classifier.joblib", "manifest.json"):
            src = out_dir / name
            if src.is_file():
                import shutil

                shutil.copy2(src, archive_dir / name)
        print(f"archived previous models -> {archive_dir}")

    x, y_anom, y_alarm = build_xy(rows)
    split_kw = dict(test_size=args.test_size, random_state=args.seed)
    try:
        x_train, x_test, ya_train, ya_test, yc_train, yc_test = train_test_split(
            x, y_anom, y_alarm, stratify=y_anom, **split_kw
        )
    except ValueError:
        x_train, x_test, ya_train, ya_test, yc_train, yc_test = train_test_split(
            x, y_anom, y_alarm, **split_kw
        )

    clf_anom = RandomForestClassifier(
        n_estimators=120, max_depth=12, class_weight="balanced", random_state=args.seed
    )
    clf_anom.fit(x_train, ya_train)
    anom_pred = clf_anom.predict(x_test)
    anom_acc = accuracy_score(ya_test, anom_pred)

    clf_alarm = RandomForestClassifier(
        n_estimators=100, max_depth=10, class_weight="balanced", random_state=args.seed
    )
    clf_alarm.fit(x_train, yc_train)
    alarm_pred = clf_alarm.predict(x_test)
    alarm_acc = accuracy_score(yc_test, alarm_pred)

    joblib.dump(clf_anom, out_dir / "anomaly_classifier.joblib")
    joblib.dump(clf_alarm, out_dir / "alarm_classifier.joblib")

    manifest = {
        "version": "1",
        "trained_at": datetime.now(timezone.utc).isoformat(),
        "source": "sim_jsonl" if "training_dataset" in dataset.name else "custom",
        "dataset": str(dataset.resolve()),
        "rows": len(rows),
        "feature_names": FEATURE_NAMES,
        "alarm_classes": list(clf_alarm.classes_),
        "metrics": {
            "anomaly_accuracy": round(float(anom_acc), 4),
            "alarm_accuracy": round(float(alarm_acc), 4),
        },
        "alarm_report": classification_report(yc_test, alarm_pred, zero_division=0),
    }
    (out_dir / "manifest.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")

    print(f"saved: {out_dir / 'anomaly_classifier.joblib'}")
    print(f"saved: {out_dir / 'alarm_classifier.joblib'}")
    print(json.dumps(manifest["metrics"], indent=2))


if __name__ == "__main__":
    main()
