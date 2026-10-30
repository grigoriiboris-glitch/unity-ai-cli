import { mkdir } from "node:fs/promises";
import path from "node:path";
import pixelmatch from "pixelmatch";
import sharp from "sharp";

const MAX_IMAGE_PIXELS = 32_000_000;

async function readRgba(filePath) {
  const { data, info } = await sharp(filePath, { failOn: "error", limitInputPixels: MAX_IMAGE_PIXELS })
    .rotate()
    .ensureAlpha()
    .raw()
    .toBuffer({ resolveWithObject: true });
  if (info.width * info.height > MAX_IMAGE_PIXELS) {
    throw new Error(`Image exceeds the ${MAX_IMAGE_PIXELS.toLocaleString()} pixel safety limit`);
  }
  return { data, width: info.width, height: info.height };
}

function connectedRegions(mask, width, height) {
  const total = width * height;
  const stack = new Int32Array(total);
  let regions = 0;
  let changedPixels = 0;
  let minX = width;
  let minY = height;
  let maxX = -1;
  let maxY = -1;

  for (let start = 0; start < total; start += 1) {
    if (!mask[start]) continue;
    regions += 1;
    let stackSize = 0;
    stack[stackSize++] = start;
    mask[start] = 0;
    while (stackSize) {
      const index = stack[--stackSize];
      changedPixels += 1;
      const x = index % width;
      const y = Math.floor(index / width);
      if (x < minX) minX = x;
      if (x > maxX) maxX = x;
      if (y < minY) minY = y;
      if (y > maxY) maxY = y;
      const neighbors = [
        x > 0 ? index - 1 : -1,
        x + 1 < width ? index + 1 : -1,
        y > 0 ? index - width : -1,
        y + 1 < height ? index + width : -1
      ];
      for (const next of neighbors) {
        if (next >= 0 && mask[next]) {
          mask[next] = 0;
          stack[stackSize++] = next;
        }
      }
    }
  }
  return {
    regions,
    changedPixels,
    changed_bbox: maxX < 0 ? null : [minX, minY, maxX + 1, maxY + 1]
  };
}

export async function compareImages(baselinePath, currentPath, {
  threshold = 0.06,
  outputPath = null,
  maxPixels = MAX_IMAGE_PIXELS
} = {}) {
  const baseline = await readRgba(baselinePath);
  const current = await readRgba(currentPath);
  if (baseline.width !== current.width || baseline.height !== current.height) {
    return {
      status: "failed",
      error: `image_size_mismatch: ${baseline.width}x${baseline.height} != ${current.width}x${current.height}`
    };
  }
  const pixels = baseline.width * baseline.height;
  if (pixels > maxPixels) throw new Error(`Image exceeds the ${maxPixels.toLocaleString()} pixel limit`);
  const diff = Buffer.alloc(pixels * 4);
  const diffPixels = pixelmatch(
    baseline.data,
    current.data,
    diff,
    baseline.width,
    baseline.height,
    { threshold: Math.max(0, Math.min(1, Number(threshold))), includeAA: true, diffMask: true }
  );
  const mask = new Uint8Array(pixels);
  for (let i = 0; i < pixels; i += 1) mask[i] = diff[i * 4 + 3] > 0 ? 1 : 0;
  const regions = connectedRegions(mask, baseline.width, baseline.height);
  const diffPercent = pixels ? diffPixels / pixels * 100 : 0;
  const severity = diffPercent >= 10 ? "error" : diffPercent >= 2 ? "warning" : "info";

  let diffImage = null;
  if (outputPath) {
    await mkdir(path.dirname(path.resolve(outputPath)), { recursive: true });
    await sharp(diff, { raw: { width: baseline.width, height: baseline.height, channels: 4 } })
      .png()
      .toFile(outputPath);
    diffImage = path.resolve(outputPath);
  }

  return {
    status: "ok",
    baseline: path.basename(baselinePath),
    current: path.basename(currentPath),
    width: baseline.width,
    height: baseline.height,
    diff_percent: Number(diffPercent.toFixed(3)),
    changed_pixels: diffPixels,
    mean_delta: null,
    changed_regions: regions.regions,
    severity,
    changed_bbox: regions.changed_bbox,
    diff_image: diffImage
  };
}

export async function selectFrames(directory, { maxFrames = 5, threshold = 0.25 } = {}) {
  const limit = Number(maxFrames);
  if (!Number.isInteger(limit) || limit < 1 || limit > 100) {
    return { status: "failed", error: "max_frames must be an integer between 1 and 100" };
  }
  const { readdir } = await import("node:fs/promises");
  const entries = await readdir(directory, { withFileTypes: true });
  const files = entries
    .filter((entry) => entry.isFile() && /\.(png|jpe?g|webp)$/i.test(entry.name))
    .map((entry) => entry.name)
    .sort((a, b) => a.localeCompare(b, undefined, { numeric: true }));
  if (!files.length) return { status: "ok", selected_frames: [], total_frames: 0 };
  if (files.length <= limit) {
    return { status: "ok", total_frames: files.length, selected_frames: files, selected_indices: files.map((_, i) => i) };
  }

  const scores = [];
  let previous = await readRgba(path.join(directory, files[0]));
  for (let index = 1; index < files.length; index += 1) {
    const current = await readRgba(path.join(directory, files[index]));
    let score;
    if (current.width !== previous.width || current.height !== previous.height) {
      score = 100;
    } else {
      score = pixelmatch(previous.data, current.data, null, current.width, current.height, {
        threshold: 0.06,
        includeAA: true
      }) / (current.width * current.height) * 100;
    }
    scores.push({ index, score });
    previous = current;
  }

  const candidates = new Set([0, files.length - 1]);
  for (const item of [...scores].sort((a, b) => b.score - a.score || a.index - b.index)) {
    if (item.score >= Number(threshold)) candidates.add(item.index);
    if (candidates.size >= limit) break;
  }
  if (candidates.size < limit) {
    for (let index = 0; index < files.length && candidates.size < limit; index += 1) candidates.add(index);
  }
  const selectedIndices = [...candidates].sort((a, b) => a - b).slice(0, limit);
  return {
    status: "ok",
    total_frames: files.length,
    selected_frames: selectedIndices.map((index) => files[index]),
    selected_indices: selectedIndices,
    max_frames: limit,
    threshold_percent: Number(threshold)
  };
}
