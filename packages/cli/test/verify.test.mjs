import assert from "node:assert/strict";
import { mkdtemp, mkdir, rm, writeFile } from "node:fs/promises";
import os from "node:os";
import path from "node:path";
import test from "node:test";
import { verifyProject } from "../src/verify.mjs";

async function makeProject(t, report) {
  const root = await mkdtemp(path.join(os.tmpdir(), "unity-ai-verify-"));
  t.after(() => rm(root, { recursive: true, force: true }));
  await mkdir(path.join(root, ".ai", "artifacts"), { recursive: true });
  await writeFile(path.join(root, "Editor.log"), "Editor loaded without errors\n");
  await writeFile(path.join(root, ".ai", "artifacts", "verification-report.json"), JSON.stringify({
    generatedAt: new Date().toISOString(),
    checkedSuites: ["physics", "ui", "visibility"],
    physics: { status: "passed", checkedObjects: 1 },
    ui: { status: "passed", checkedObjects: 1 },
    visibility: { status: "passed", checkedObjects: 1 },
    anomalies: [],
    skipped: [],
    ...report
  }));
  return root;
}

test("physics-only verification is not blocked by an unrelated skipped UI suite", async (t) => {
  const root = await makeProject(t, {
    status: "blocked",
    ui: { status: "requires_play_mode", checkedObjects: 1 }
  });
  const result = await verifyProject(root, {
    suites: ["physics"],
    editorLog: path.join(root, "Editor.log")
  });
  assert.equal(result.status, "passed");
});

test("UI verification is blocked until Play Mode is available", async (t) => {
  const root = await makeProject(t, {
    status: "blocked",
    ui: { status: "requires_play_mode", checkedObjects: 1 }
  });
  const result = await verifyProject(root, {
    suites: ["physics", "ui"],
    editorLog: path.join(root, "Editor.log")
  });
  assert.equal(result.status, "failed");
  assert.deepEqual(result.checks.unity.blockedSuites, ["ui"]);
});

test("severity field from Unity JsonUtility is respected", async (t) => {
  const root = await makeProject(t, {
    status: "passed",
    anomalies: [{ Id: "UI-001", Severity: "warning", Message: "Expected warning" }]
  });
  const result = await verifyProject(root, {
    suites: ["physics"],
    editorLog: path.join(root, "Editor.log")
  });
  assert.equal(result.status, "passed");
  assert.equal(result.checks.unity.errors.length, 0);
});
