import assert from "node:assert/strict";
import { execFileSync } from "node:child_process";
import { mkdir, mkdtemp, rm, writeFile } from "node:fs/promises";
import os from "node:os";
import path from "node:path";
import test from "node:test";
import { createCheckpoint } from "../src/checkpoint.mjs";

async function makeGitRepo(t) {
  const root = await mkdtemp(path.join(os.tmpdir(), "unity-ai-checkpoint-"));
  t.after(() => rm(root, { recursive: true, force: true }));
  const git = (...args) => execFileSync("git", args, {
    cwd: root,
    encoding: "utf8",
    stdio: ["ignore", "pipe", "pipe"]
  });
  git("init", "-q");
  git("config", "user.name", "Unity AI Tests");
  git("config", "user.email", "unity-ai-tests@example.invalid");
  await writeFile(path.join(root, ".gitignore"), ".ai/artifacts/\n.ai/checkpoints/\n");
  await writeFile(path.join(root, "README.md"), "checkpoint base\n");
  git("add", "-A");
  git("commit", "-m", "test base");
  await mkdir(path.join(root, ".ai", "artifacts"), { recursive: true });
  return { root, git };
}

async function writeSaveState(root, overrides = {}) {
  const state = {
    status: "saved",
    savedAt: new Date().toISOString(),
    scenesSaved: true,
    assetsSaved: true,
    unsavedSceneCount: 0,
    scenes: [],
    ...overrides
  };
  await writeFile(path.join(root, ".ai", "artifacts", "editor-save-state.json"), JSON.stringify(state));
}

test("checkpoint refuses when no fresh Unity save-state file exists", async (t) => {
  const { root } = await makeGitRepo(t);
  await assert.rejects(
    () => createCheckpoint(root, "unsafe"),
    /Unity save confirmation is missing/
  );
});

test("checkpoint refuses when Unity still reports dirty scenes", async (t) => {
  const { root } = await makeGitRepo(t);
  await writeSaveState(root, { status: "blocked", scenesSaved: false, unsavedSceneCount: 1 });
  await assert.rejects(() => createCheckpoint(root, "unsafe"), /unsaved scenes or assets/);
});

test("checkpoint commits saved files and creates an annotated local tag", async (t) => {
  const { root, git } = await makeGitRepo(t);
  await writeSaveState(root);
  await writeFile(path.join(root, "Assets.txt"), "saved by Unity\n");
  const result = await createCheckpoint(root, "before-ui-refactor");
  assert.equal(result.status, "ok");
  assert.equal(result.commitCreated, true);
  assert.equal(git("tag", "--list", result.tag).trim(), result.tag);
  assert.equal(git("status", "--porcelain").trim(), "");
});
