#!/usr/bin/env python3
import argparse
import json
from pathlib import Path

from PIL import Image, ImageChops, ImageStat


def frame_score(previous: Image.Image, current: Image.Image) -> float:
    diff = ImageChops.difference(previous, current)
    return sum(ImageStat.Stat(diff).mean) / 3.0


def select_frames(directory: Path, max_frames: int, threshold: float):
    paths = sorted(
        p for p in directory.iterdir()
        if p.suffix.lower() in {".png", ".jpg", ".jpeg"}
    )

    if not paths:
        return {"status": "ok", "selected_frames": [], "total_frames": 0}

    if max_frames < 1:
        return {"status": "failed", "error": "max_frames must be >= 1"}

    if len(paths) <= max_frames:
        return {
            "status": "ok",
            "selected_frames": [p.name for p in paths],
            "total_frames": len(paths),
        }

    scores = []
    previous = Image.open(paths[0]).convert("RGB")
    for index in range(1, len(paths)):
        current = Image.open(paths[index]).convert("RGB")
        if current.size != previous.size:
            score = float("inf")
        else:
            score = frame_score(previous, current)
        scores.append((score, index))
        previous.close()
        previous = current

    candidates = {0, len(paths) - 1}
    for score, index in sorted(scores, reverse=True):
        if score >= threshold:
            candidates.add(index)
        if len(candidates) >= max_frames:
            break

    if len(candidates) < max_frames:
        for index in range(len(paths)):
            candidates.add(index)
            if len(candidates) >= max_frames:
                break

    selected = sorted(candidates)[:max_frames]
    return {
        "status": "ok",
        "total_frames": len(paths),
        "selected_frames": [paths[i].name for i in selected],
        "selected_indices": selected,
    }


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("directory", type=Path)
    parser.add_argument("--max-frames", type=int, default=5)
    parser.add_argument("--threshold", type=float, default=8.0)
    args = parser.parse_args()

    print(json.dumps(
        select_frames(args.directory, args.max_frames, max(0.0, args.threshold)),
        separators=(",", ":"),
    ))


if __name__ == "__main__":
    main()
