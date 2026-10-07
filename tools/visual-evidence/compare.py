#!/usr/bin/env python3
import argparse
import json
import math
from pathlib import Path

from PIL import Image, ImageChops, ImageStat


def compare(baseline_path: Path, current_path: Path, threshold: int):
    baseline = Image.open(baseline_path).convert("RGB")
    current = Image.open(current_path).convert("RGB")

    if baseline.size != current.size:
        return {
            "status": "failed",
            "error": f"image_size_mismatch: {baseline.size} != {current.size}",
        }

    diff = ImageChops.difference(baseline, current)
    stat = ImageStat.Stat(diff)

    pixels = baseline.width * baseline.height
    changed = 0
    bbox = diff.getbbox()

    if bbox:
        changed = sum(
            1
            for pixel in diff.getdata()
            if max(pixel) >= threshold
        )

    diff_percent = (changed / pixels * 100.0) if pixels else 0.0
    mean_delta = sum(stat.mean) / 3.0

    severity = "info"
    if diff_percent >= 10:
        severity = "error"
    elif diff_percent >= 2:
        severity = "warning"

    return {
        "status": "ok",
        "baseline": baseline_path.name,
        "current": current_path.name,
        "width": baseline.width,
        "height": baseline.height,
        "diff_percent": round(diff_percent, 3),
        "mean_delta": round(mean_delta, 3),
        "changed_regions": 1 if bbox else 0,
        "severity": severity,
        "changed_bbox": list(bbox) if bbox else None,
    }


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("baseline", type=Path)
    parser.add_argument("current", type=Path)
    parser.add_argument("--threshold", type=int, default=16)
    args = parser.parse_args()

    print(json.dumps(
        compare(args.baseline, args.current, max(0, min(255, args.threshold))),
        separators=(",", ":"),
    ))


if __name__ == "__main__":
    main()
