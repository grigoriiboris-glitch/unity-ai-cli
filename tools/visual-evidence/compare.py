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
    mask = []
    for y in range(baseline.height):
        row = []
        for x in range(baseline.width):
            row.append(max(diff.getpixel((x, y))) >= threshold)
        mask.append(row)

    regions = 0
    for y in range(baseline.height):
        for x in range(baseline.width):
            if not mask[y][x]:
                continue
            regions += 1
            stack = [(x, y)]
            mask[y][x] = False
            while stack:
                cx, cy = stack.pop()
                changed += 1
                for nx, ny in ((cx - 1, cy), (cx + 1, cy), (cx, cy - 1), (cx, cy + 1)):
                    if 0 <= nx < baseline.width and 0 <= ny < baseline.height and mask[ny][nx]:
                        mask[ny][nx] = False
                        stack.append((nx, ny))

    bbox = diff.getbbox()
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
        "changed_regions": regions,
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
