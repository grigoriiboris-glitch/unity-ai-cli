import assert from "node:assert/strict";
import { mkdtempSync, rmSync } from "node:fs";
import os from "node:os";
import path from "node:path";
import test from "node:test";
import { DiagnosticsBroker } from "../src/db.mjs";

function temporaryBroker(t) {
  const directory = mkdtempSync(path.join(os.tmpdir(), "unity-ai-db-"));
  const broker = new DiagnosticsBroker(path.join(directory, "diagnostics.sqlite3"));
  t.after(() => {
    broker.close();
    rmSync(directory, { recursive: true, force: true });
  });
  return broker;
}

test("deduplicates repeated exceptions and stores full stack only in detail", (t) => {
  const broker = temporaryBroker(t);
  const taskId = broker.newTaskId(new Date("2026-10-30T10:00:00.000Z"));
  const event = {
    severity: "error",
    category: "exception",
    message: "NullReferenceException: Player id 42 at 2026-10-07T12:00:00Z",
    exception_type: "NullReferenceException",
    file: "Assets/PlayerController.cs",
    line: 184,
    top_user_frame: "PlayerController.Update",
    stacktrace: "at PlayerController.Update () in Assets/PlayerController.cs:184",
    task_id: taskId,
    run_id: "RUN-001",
    git_commit: "abc123",
    raw_log_path: ".ai/diagnostics/raw/editor.log"
  };
  const firstId = broker.ingest(event);
  for (let index = 0; index < 999; index += 1) broker.ingest(event);
  const summary = broker.diagnosticsSummary({ taskId });
  assert.equal(summary.count, 1);
  assert.equal(summary.diagnostics[0].count, 1000);
  assert.equal(summary.diagnostics[0].id, firstId);
  assert.equal(JSON.stringify(summary).includes("PlayerController.Update"), false);
  assert.equal(broker.diagnosticsDetail(firstId).diagnostic.stacktrace, event.stacktrace);
});

test("task IDs are sequential per UTC date", (t) => {
  const broker = temporaryBroker(t);
  const fixed = new Date("2026-10-30T10:00:00.000Z");
  assert.equal(broker.newTaskId(fixed), "TASK-20261030-001");
  assert.equal(broker.newTaskId(fixed), "TASK-20261030-002");
});

test("same fingerprint is correlated to multiple tasks and clear is scoped", (t) => {
  const broker = temporaryBroker(t);
  const base = {
    severity: "error",
    category: "exception",
    message: "Object reference not set to an instance of an object",
    exception_type: "NullReferenceException",
    file: "Assets/Game.cs",
    line: 10,
    top_user_frame: "Game.Update"
  };
  broker.ingest({ ...base, task_id: "TASK-A", run_id: "RUN-1" });
  broker.ingest({ ...base, task_id: "TASK-B", run_id: "RUN-2" });
  assert.equal(broker.diagnosticsSince("TASK-A").count, 1);
  assert.equal(broker.diagnosticsSince("TASK-B").count, 1);
  assert.equal(broker.diagnosticsClear("TASK-A").cleared_occurrences, 1);
  assert.equal(broker.diagnosticsSince("TASK-A").count, 0);
  assert.equal(broker.diagnosticsSince("TASK-B").count, 1);
});

test("summary output is bounded", (t) => {
  const broker = temporaryBroker(t);
  for (let index = 0; index < 50; index += 1) {
    broker.ingest({
      severity: "error",
      category: "exception",
      message: `Different failure token-${String(index).padStart(2, "0")}`,
      exception_type: "Exception"
    });
  }
  const summary = broker.diagnosticsSummary({ pageSize: 20, maxChars: 500 });
  assert.ok(JSON.stringify(summary).length <= 500);
  assert.equal(summary.truncated, true);
});
