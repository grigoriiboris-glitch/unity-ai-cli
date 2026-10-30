import { createHash } from "node:crypto";
import { mkdirSync } from "node:fs";
import path from "node:path";
import { DatabaseSync } from "node:sqlite";

export const DEFAULT_MAX_DIAGNOSTICS = 20;
export const DEFAULT_MAX_CHARS = 3000;

const UUID_RE = /\b[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}\b/gi;
const HEX_RE = /\b0x[0-9a-f]+\b/gi;
const ISO_TIME_RE = /\b\d{4}-\d{2}-\d{2}[T ]\d{2}:\d{2}:\d{2}(?:\.\d+)?(?:Z|[+-]\d{2}:?\d{2})?\b/g;
const NUMBER_RE = /(?<![A-Za-z_])[-+]?\d+(?:\.\d+)?(?![A-Za-z_])/g;
const FRAME_RE = /^\s*at\s+(.+?)\s*(?:\((.*)\))?\s*$/;
const PATH_LINE_RE = /(.*?):(\d+)(?::\d+)?$/;

export function nowIso() {
  return new Date().toISOString();
}

export function normalizeMessage(message = "") {
  return String(message)
    .replace(ISO_TIME_RE, "<time>")
    .replace(UUID_RE, "<uuid>")
    .replace(HEX_RE, "<hex>")
    .replace(NUMBER_RE, "<n>")
    .replace(/\s+/g, " ")
    .trim();
}

export function extractSource(message = "", stacktrace = "") {
  for (const source of [message, stacktrace]) {
    for (const rawLine of String(source).split(/\r?\n/)) {
      let candidate = rawLine.trim();
      if (candidate.includes(" in ")) candidate = candidate.slice(candidate.lastIndexOf(" in ") + 4).trim();
      const match = PATH_LINE_RE.exec(candidate);
      if (match) return { file: match[1], line: Number(match[2]) };
    }
  }
  return { file: "", line: null };
}

export function findTopUserFrame(stacktrace = "") {
  const ignored = ["UnityEngine.", "UnityEditor.", "System.", "Microsoft.", "Mono.", "mscorlib"];
  for (const rawLine of String(stacktrace).split(/\r?\n/)) {
    const match = FRAME_RE.exec(rawLine);
    let frame = (match?.[1] ?? rawLine.trim()).trim();
    if (frame.includes(" in ")) frame = frame.slice(0, frame.lastIndexOf(" in ")).trim();
    frame = frame.replace(/\s*\([^)]*\)$/, "").trim();
    if (frame && !ignored.some((prefix) => frame.startsWith(prefix))) return frame;
  }
  return "";
}

export function fingerprintFor(event) {
  const source = extractSource(event.message, event.stacktrace);
  const file = event.file || source.file;
  const line = event.line ?? source.line ?? "";
  const topFrame = event.top_user_frame || findTopUserFrame(event.stacktrace);
  const normalizedStack = String(event.stacktrace || "")
    .split(/\r?\n/)
    .map((part) => part.trim().replace(/\s+/g, " "))
    .filter(Boolean)
    .join("\n");
  const payload = [
    String(event.exception_type || "").trim(),
    normalizeMessage(event.message),
    String(file).trim(),
    String(line),
    topFrame.trim(),
    normalizedStack
  ].join("\n");
  return createHash("sha256").update(payload, "utf8").digest("hex").slice(0, 24);
}

export function eventFromMapping(data) {
  if (!data || typeof data !== "object" || Array.isArray(data)) {
    throw new TypeError("Diagnostic event must be a JSON object");
  }
  const line = data.line == null ? null : Number(data.line);
  if (line !== null && (!Number.isSafeInteger(line) || line < 1)) {
    throw new TypeError("Diagnostic event line must be a positive integer");
  }
  return {
    severity: String(data.severity ?? "error"),
    category: String(data.category ?? ""),
    message: String(data.message ?? ""),
    exception_type: String(data.exception_type ?? ""),
    file: String(data.file ?? ""),
    line,
    top_user_frame: String(data.top_user_frame ?? ""),
    stacktrace: String(data.stacktrace ?? ""),
    task_id: String(data.task_id ?? ""),
    run_id: String(data.run_id ?? ""),
    git_commit: String(data.git_commit ?? ""),
    raw_log_path: String(data.raw_log_path ?? ""),
    timestamp: String(data.timestamp ?? "")
  };
}

export class DiagnosticsBroker {
  constructor(dbPath = ".ai/diagnostics/diagnostics.sqlite3") {
    this.dbPath = path.resolve(dbPath);
    mkdirSync(path.dirname(this.dbPath), { recursive: true });
    this.db = new DatabaseSync(this.dbPath);
    this.db.exec("PRAGMA journal_mode=WAL; PRAGMA foreign_keys=ON;");
    this.db.exec(`
      CREATE TABLE IF NOT EXISTS diagnostics (
        id TEXT PRIMARY KEY,
        fingerprint TEXT NOT NULL UNIQUE,
        severity TEXT NOT NULL,
        category TEXT,
        message TEXT,
        file TEXT,
        line INTEGER,
        count INTEGER NOT NULL DEFAULT 1,
        first_seen TEXT NOT NULL,
        last_seen TEXT NOT NULL,
        task_id TEXT,
        git_commit TEXT,
        raw_log_path TEXT,
        exception_type TEXT,
        top_user_frame TEXT,
        stacktrace TEXT
      );
      CREATE TABLE IF NOT EXISTS diagnostic_occurrences (
        id INTEGER PRIMARY KEY AUTOINCREMENT,
        diagnostic_id TEXT NOT NULL REFERENCES diagnostics(id) ON DELETE CASCADE,
        task_id TEXT NOT NULL DEFAULT '',
        run_id TEXT NOT NULL DEFAULT '',
        git_commit TEXT,
        seen_at TEXT NOT NULL
      );
      CREATE INDEX IF NOT EXISTS idx_diag_last_seen ON diagnostics(last_seen DESC);
      CREATE INDEX IF NOT EXISTS idx_diag_task ON diagnostic_occurrences(task_id);
      CREATE INDEX IF NOT EXISTS idx_diag_run ON diagnostic_occurrences(run_id);
      CREATE TABLE IF NOT EXISTS task_sequences (
        utc_date TEXT PRIMARY KEY,
        next_number INTEGER NOT NULL
      );
    `);
  }

  close() {
    if (this.db?.isOpen) this.db.close();
  }

  newTaskId(now = new Date()) {
    const dateKey = now.toISOString().slice(0, 10).replaceAll("-", "");
    const row = this.db.prepare(`
      INSERT INTO task_sequences (utc_date, next_number)
      VALUES (?, 2)
      ON CONFLICT(utc_date) DO UPDATE
        SET next_number = task_sequences.next_number + 1
      RETURNING next_number - 1 AS sequence
    `).get(dateKey);
    return `TASK-${dateKey}-${String(row.sequence).padStart(3, "0")}`;
  }

  ingest(input) {
    const event = eventFromMapping(input);
    const timestamp = event.timestamp || nowIso();
    const fingerprint = fingerprintFor(event);
    const id = `DIAG-${fingerprint.toUpperCase()}`;
    const source = extractSource(event.message, event.stacktrace);
    const file = event.file || source.file;
    const line = event.line ?? source.line;
    const topFrame = event.top_user_frame || findTopUserFrame(event.stacktrace);
    const severity = event.severity.trim().toLowerCase() || "error";

    this.db.prepare(`
      INSERT INTO diagnostics (
        id, fingerprint, severity, category, message, file, line, count,
        first_seen, last_seen, task_id, git_commit, raw_log_path,
        exception_type, top_user_frame, stacktrace
      )
      VALUES (?, ?, ?, ?, ?, ?, ?, 1, ?, ?, ?, ?, ?, ?, ?, ?)
      ON CONFLICT(fingerprint) DO UPDATE SET
        count = diagnostics.count + 1,
        last_seen = excluded.last_seen,
        severity = excluded.severity,
        category = excluded.category,
        message = excluded.message,
        file = excluded.file,
        line = excluded.line,
        task_id = excluded.task_id,
        git_commit = excluded.git_commit,
        raw_log_path = excluded.raw_log_path,
        exception_type = excluded.exception_type,
        top_user_frame = excluded.top_user_frame,
        stacktrace = excluded.stacktrace
    `).run(
      id, fingerprint, severity, event.category, event.message.trim(), file, line,
      timestamp, timestamp, event.task_id, event.git_commit, event.raw_log_path,
      event.exception_type, topFrame, event.stacktrace
    );

    if (event.task_id || event.run_id) {
      this.db.prepare(`
        INSERT INTO diagnostic_occurrences (diagnostic_id, task_id, run_id, git_commit, seen_at)
        VALUES (?, ?, ?, ?, ?)
      `).run(id, event.task_id, event.run_id, event.git_commit, timestamp);
    }
    return id;
  }

  queryRows({ taskId = "", query = "", since = "", limit = DEFAULT_MAX_DIAGNOSTICS, offset = 0 } = {}) {
    const clauses = [];
    const params = [];
    if (taskId) {
      clauses.push("EXISTS (SELECT 1 FROM diagnostic_occurrences occ WHERE occ.diagnostic_id = d.id AND occ.task_id = ?)");
      params.push(taskId);
    }
    if (query) {
      clauses.push("(d.id LIKE ? OR d.fingerprint LIKE ? OR d.message LIKE ? OR d.file LIKE ? OR d.exception_type LIKE ?)");
      const needle = `%${query}%`;
      params.push(needle, needle, needle, needle, needle);
    }
    if (since) {
      clauses.push("d.last_seen >= ?");
      params.push(since);
    }
    const where = clauses.length ? `WHERE ${clauses.join(" AND ")}` : "";
    params.push(Math.max(1, Number(limit) || 1), Math.max(0, Number(offset) || 0));
    return this.db.prepare(`
      SELECT d.* FROM diagnostics d
      ${where}
      ORDER BY d.last_seen DESC
      LIMIT ? OFFSET ?
    `).all(...params);
  }

  summaryRow(row) {
    return {
      id: row.id,
      fingerprint: row.fingerprint,
      severity: row.severity,
      category: row.category,
      message: row.message,
      file: row.file,
      line: row.line,
      count: row.count,
      first_seen: row.first_seen,
      last_seen: row.last_seen,
      task_id: row.task_id,
      git_commit: row.git_commit
    };
  }

  diagnosticsSummary({ taskId = "", page = 1, pageSize = DEFAULT_MAX_DIAGNOSTICS, maxChars = DEFAULT_MAX_CHARS } = {}) {
    const currentPage = Math.max(1, Number(page) || 1);
    const size = Math.max(1, Math.min(Number(pageSize) || DEFAULT_MAX_DIAGNOSTICS, DEFAULT_MAX_DIAGNOSTICS));
    const rows = this.queryRows({ taskId, limit: size + 1, offset: (currentPage - 1) * size });
    const hasMore = rows.length > size;
    const diagnostics = rows.slice(0, size).map((row) => this.summaryRow(row));
    return this.boundedJson({
      status: "ok", count: diagnostics.length, page: currentPage,
      page_size: size, has_more: hasMore, diagnostics
    }, maxChars);
  }

  diagnosticsSearch(query, { page = 1, pageSize = DEFAULT_MAX_DIAGNOSTICS, maxChars = DEFAULT_MAX_CHARS } = {}) {
    const currentPage = Math.max(1, Number(page) || 1);
    const size = Math.max(1, Math.min(Number(pageSize) || DEFAULT_MAX_DIAGNOSTICS, DEFAULT_MAX_DIAGNOSTICS));
    const rows = this.queryRows({ query, limit: size + 1, offset: (currentPage - 1) * size });
    const hasMore = rows.length > size;
    const diagnostics = rows.slice(0, size).map((row) => this.summaryRow(row));
    return this.boundedJson({
      status: "ok", query, count: diagnostics.length, page: currentPage,
      page_size: size, has_more: hasMore, diagnostics
    }, maxChars);
  }

  diagnosticsDetail(diagnosticId) {
    const row = this.db.prepare("SELECT * FROM diagnostics WHERE id = ?").get(diagnosticId);
    if (!row) return { status: "not_found", id: diagnosticId };
    return {
      status: "ok",
      diagnostic: {
        ...this.summaryRow(row),
        exception_type: row.exception_type,
        top_user_frame: row.top_user_frame,
        raw_log_path: row.raw_log_path,
        stacktrace: row.stacktrace
      }
    };
  }

  diagnosticsSince(taskId, { maxChars = DEFAULT_MAX_CHARS } = {}) {
    const rows = this.queryRows({ taskId, limit: DEFAULT_MAX_DIAGNOSTICS + 1 });
    const hasMore = rows.length > DEFAULT_MAX_DIAGNOSTICS;
    const diagnostics = rows.slice(0, DEFAULT_MAX_DIAGNOSTICS).map((row) => this.summaryRow(row));
    return this.boundedJson({
      status: "ok", task_id: taskId, count: diagnostics.length,
      has_more: hasMore, diagnostics
    }, maxChars);
  }

  diagnosticsClear(taskId) {
    const result = this.db.prepare("DELETE FROM diagnostic_occurrences WHERE task_id = ?").run(taskId);
    return { status: "ok", task_id: taskId, cleared_occurrences: Number(result.changes) };
  }

  boundedJson(value, maxChars = DEFAULT_MAX_CHARS) {
    const limit = Math.max(128, Number(maxChars) || DEFAULT_MAX_CHARS);
    if (JSON.stringify(value).length <= limit) return value;
    const diagnostics = [...(value.diagnostics ?? [])];
    while (diagnostics.length) {
      diagnostics.pop();
      const candidate = {
        ...value,
        diagnostics,
        count: diagnostics.length,
        truncated: true
      };
      if (JSON.stringify(candidate).length <= limit) return candidate;
    }
    return { status: "truncated", count: 0, has_more: Boolean(value.count), truncated: true, diagnostics: [] };
  }
}

export async function loadJsonLines(filePath) {
  const { readFile } = await import("node:fs/promises");
  const content = await readFile(filePath, "utf8");
  return content.split(/\r?\n/).filter((line) => line.trim()).map((line, index) => {
    try {
      return eventFromMapping(JSON.parse(line));
    } catch (error) {
      throw new Error(`Invalid JSONL event at line ${index + 1}: ${error.message}`, { cause: error });
    }
  });
}
