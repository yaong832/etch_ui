#!/usr/bin/env python3
"""CSV 데이터셋으로 간단한 베이스라인 모델(규칙 기반) 학습/평가."""

from __future__ import annotations

import argparse
import csv
import json
from pathlib import Path
from statistics import mean


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Train and evaluate baseline alarm model")
    parser.add_argument(
        "--dataset",
        default="tools/ai/output/training_dataset.csv",
        help="Input dataset csv path",
    )
    parser.add_argument(
        "--model-out",
        default="tools/ai/output/baseline_model.json",
        help="Output model json path",
    )
    return parser.parse_args()


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
            rows.append(row)
        return rows


def split_train_test(rows: list[dict], ratio: float = 0.8) -> tuple[list[dict], list[dict]]:
    pivot = int(len(rows) * ratio)
    return rows[:pivot], rows[pivot:]


def fit_thresholds(train_rows: list[dict]) -> dict:
    normal = [r for r in train_rows if r["label_alarm"] == 0]
    if not normal:
        normal = train_rows
    return {
        "pressure_min": min(r["pressure"] for r in normal),
        "pressure_max": max(r["pressure"] for r in normal),
        "vibration_max": max(r["vibration"] for r in normal),
        "temperature_min": min(r["temperature"] for r in normal),
        "temperature_max": max(r["temperature"] for r in normal),
        "humidity_min": min(r["humidity"] for r in normal),
        "humidity_max": max(r["humidity"] for r in normal),
    }


def predict(row: dict, model: dict) -> int:
    if row["interlock_ok"] == 0:
        return 1
    if row["access_safe"] == 0:
        return 1
    if row["module_alarm_count"] > 0:
        return 1
    if not (model["pressure_min"] <= row["pressure"] <= model["pressure_max"]):
        return 1
    if row["vibration"] > model["vibration_max"]:
        return 1
    if not (model["temperature_min"] <= row["temperature"] <= model["temperature_max"]):
        return 1
    if not (model["humidity_min"] <= row["humidity"] <= model["humidity_max"]):
        return 1
    return 0


def evaluate(rows: list[dict], model: dict) -> dict:
    if not rows:
        return {"count": 0, "accuracy": 0.0, "precision": 0.0, "recall": 0.0}

    tp = fp = tn = fn = 0
    for row in rows:
        pred = predict(row, model)
        y = row["label_alarm"]
        if pred == 1 and y == 1:
            tp += 1
        elif pred == 1 and y == 0:
            fp += 1
        elif pred == 0 and y == 0:
            tn += 1
        else:
            fn += 1

    accuracy = (tp + tn) / len(rows)
    precision = tp / (tp + fp) if (tp + fp) else 0.0
    recall = tp / (tp + fn) if (tp + fn) else 0.0
    return {
        "count": len(rows),
        "accuracy": accuracy,
        "precision": precision,
        "recall": recall,
        "tp": tp,
        "fp": fp,
        "tn": tn,
        "fn": fn,
    }


def main() -> None:
    args = parse_args()
    dataset_path = Path(args.dataset)
    model_path = Path(args.model_out)
    model_path.parent.mkdir(parents=True, exist_ok=True)

    if not dataset_path.exists():
        raise FileNotFoundError(f"Dataset not found: {dataset_path}")

    rows = load_rows(dataset_path)
    if len(rows) < 20:
        raise RuntimeError("Need at least 20 rows for baseline training")

    train_rows, test_rows = split_train_test(rows, ratio=0.8)
    model = fit_thresholds(train_rows)
    model["meta"] = {
        "train_rows": len(train_rows),
        "test_rows": len(test_rows),
        "alarm_ratio_train": mean([r["label_alarm"] for r in train_rows]) if train_rows else 0.0,
    }

    metrics = evaluate(test_rows, model)
    model["metrics"] = metrics

    model_path.write_text(json.dumps(model, indent=2), encoding="utf-8")

    print(f"model saved: {model_path}")
    print(json.dumps(metrics, indent=2))


if __name__ == "__main__":
    main()
