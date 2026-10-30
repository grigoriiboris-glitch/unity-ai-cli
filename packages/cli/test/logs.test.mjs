import assert from "node:assert/strict";
import { mkdtemp, mkdir, rm, writeFile } from "node:fs/promises";
import os from "node:os";
import path from "node:path";
import test from "node:test";
import { collectErrorBlocks, readSourceSnippet, summarizeEditorLog } from "../src/logs.mjs";

test("groups repeated exception stack traces and reports occurrence count", () => {
  const block = [
    "NullReferenceException: PlayerController failed",
    "at UnityEngine.Debug.LogError ()",
    "at PlayerController.Update () in Assets/PlayerController.cs:184"
  ].join("\n");
  const result = collectErrorBlocks([block, block].join("\n\n"));
  assert.equal(result.length, 1);
  assert.equal(result[0].count, 2);
  assert.equal(result[0].file, "Assets/PlayerController.cs");
  assert.equal(result[0].line, 184);
  assert.equal(result[0].top_user_frame.startsWith("PlayerController.Update"), true);
});

test("parses Unity C# compiler errors with the full Assets path", () => {
  const result = collectErrorBlocks(
    "Assets/Scripts/PlayerController.cs(42,7): error CS0103: The name 'health' does not exist in the current context"
  );
  assert.equal(result.length, 1);
  assert.equal(result[0].category, "compile");
  assert.equal(result[0].file, "Assets/Scripts/PlayerController.cs");
  assert.equal(result[0].line, 42);
  assert.equal(result[0].exception_type, "CS0103");
});

test("adds a bounded source excerpt around the failing line", async (t) => {
  const root = await mkdtemp(path.join(os.tmpdir(), "unity-ai-source-"));
  t.after(() => rm(root, { recursive: true, force: true }));
  await mkdir(path.join(root, "Assets"), { recursive: true });
  await writeFile(path.join(root, "Assets", "Game.cs"), Array.from({ length: 20 }, (_, i) => `line-${i + 1}`).join("\n"));
  const snippet = await readSourceSnippet(root, "Assets/Game.cs", 10, 2);
  assert.equal(snippet.start_line, 8);
  assert.equal(snippet.end_line, 12);
  assert.equal(snippet.lines.find((line) => line.target).number, 10);
});

test("summarizes only a bounded Editor.log tail", async (t) => {
  const root = await mkdtemp(path.join(os.tmpdir(), "unity-ai-log-"));
  t.after(() => rm(root, { recursive: true, force: true }));
  const log = path.join(root, "Editor.log");
  await writeFile(log, [
    "noise",
    "NullReferenceException: failure",
    "at Game.Update () in Assets/Game.cs:9",
    "",
    "noise",
    "NullReferenceException: failure",
    "at Game.Update () in Assets/Game.cs:9"
  ].join("\n"));
  const summary = await summarizeEditorLog(log, { repositoryRoot: root, includeSource: false });
  assert.equal(summary.error_groups, 1);
  assert.equal(summary.total_occurrences, 2);
});
