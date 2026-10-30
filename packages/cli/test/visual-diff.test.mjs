import assert from "node:assert/strict";
import { mkdtemp, rm } from "node:fs/promises";
import os from "node:os";
import path from "node:path";
import test from "node:test";
import sharp from "sharp";
import { compareImages, selectFrames } from "../src/visual-diff.mjs";

test("identical images have zero diff and mismatched images produce a diff image", async (t) => {
  const directory = await mkdtemp(path.join(os.tmpdir(), "unity-ai-visual-"));
  t.after(() => rm(directory, { recursive: true, force: true }));
  const baseline = path.join(directory, "baseline.png");
  const current = path.join(directory, "current.png");
  const diff = path.join(directory, "diff.png");
  await sharp({ create: { width: 10, height: 10, channels: 3, background: "#000000" } }).png().toFile(baseline);
  await sharp({ create: { width: 10, height: 10, channels: 3, background: "#000000" } }).png().toFile(current);
  const same = await compareImages(baseline, current);
  assert.equal(same.status, "ok");
  assert.equal(same.diff_percent, 0);
  await sharp({ create: { width: 10, height: 10, channels: 3, background: "#ffffff" } }).png().toFile(current);
  const changed = await compareImages(baseline, current, { outputPath: diff });
  assert.equal(changed.changed_pixels, 100);
  assert.equal(changed.changed_regions, 1);
  assert.equal(changed.severity, "error");
  assert.equal(changed.diff_image, diff);
});

test("reports dimension mismatch as a bounded failure", async (t) => {
  const directory = await mkdtemp(path.join(os.tmpdir(), "unity-ai-mismatch-"));
  t.after(() => rm(directory, { recursive: true, force: true }));
  const first = path.join(directory, "first.png");
  const second = path.join(directory, "second.png");
  await sharp({ create: { width: 8, height: 8, channels: 3, background: "#000" } }).png().toFile(first);
  await sharp({ create: { width: 9, height: 9, channels: 3, background: "#000" } }).png().toFile(second);
  const result = await compareImages(first, second);
  assert.equal(result.status, "failed");
  assert.match(result.error, /image_size_mismatch/);
});

test("frame selection keeps sequence edges and stays within the requested limit", async (t) => {
  const directory = await mkdtemp(path.join(os.tmpdir(), "unity-ai-frames-"));
  t.after(() => rm(directory, { recursive: true, force: true }));
  for (let index = 0; index < 20; index += 1) {
    const value = index === 12 ? "#ffffff" : "#000000";
    await sharp({ create: { width: 8, height: 8, channels: 3, background: value } })
      .png()
      .toFile(path.join(directory, `frame-${String(index).padStart(3, "0")}.png`));
  }
  const result = await selectFrames(directory, { maxFrames: 4, threshold: 20 });
  assert.equal(result.status, "ok");
  assert.equal(result.selected_frames.length, 4);
  assert.ok(result.selected_frames.includes("frame-000.png"));
  assert.ok(result.selected_frames.includes("frame-019.png"));
  assert.ok(result.selected_frames.includes("frame-012.png") || result.selected_frames.includes("frame-013.png"));
});
