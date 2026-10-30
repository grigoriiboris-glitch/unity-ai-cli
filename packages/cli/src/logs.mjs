import { open, stat, readFile } from "node:fs/promises";
import path from "node:path";
import { createHash } from "node:crypto";
import { extractSource, findTopUserFrame, normalizeMessage } from "./db.mjs";

const EXCEPTION_RE = /^([A-Za-z_][A-Za-z0-9_.]*(?:Exception|Error))(?::\s*(.*))?$/;
const COMPILE_ERROR_RE = /^(?:\s*)?(?:Assets\/|Packages\/|Library\/PackageCache\/)?(.+?\.cs)\((\d+),(\d+)\):\s*error\s+(CS\d+)\s*:\s*(.*)$/i;
const ERROR_RE = /^(?:Error|ERROR|error):\s*(.+)$/;
const DEFAULT_TAIL_BYTES = 2 * 1024 * 1024;

async function readTail(filePath, maxBytes = DEFAULT_TAIL_BYTES) {
  const file = await open(filePath, "r");
  try {
    const info = await file.stat();
    const length = Math.min(info.size, maxBytes);
    const buffer = Buffer.alloc(length);
    await file.read(buffer, 0, length, Math.max(0, info.size - length));
    let text = buffer.toString("utf8");
    if (info.size > length) {
      const firstNewline = text.indexOf("\n");
      text = firstNewline >= 0 ? text.slice(firstNewline + 1) : text;
    }
    return text;
  } finally {
    await file.close();
  }
}

function isHeader(line) {
  const trimmed = line.trim();
  return EXCEPTION_RE.test(trimmed) || COMPILE_ERROR_RE.test(trimmed) || ERROR_RE.test(trimmed);
}

function parseBlock(blockLines) {
  const first = blockLines[0]?.trim() ?? "";
  const compile = COMPILE_ERROR_RE.exec(first);
  const exception = EXCEPTION_RE.exec(first);
  const genericError = ERROR_RE.exec(first);
  let category = "log";
  let exceptionType = "";
  let message = first;
  let file = "";
  let line = null;

  if (compile) {
    category = "compile";
    file = compile[1];
    line = Number(compile[2]);
    exceptionType = compile[4];
    message = compile[5];
  } else if (exception) {
    category = "exception";
    exceptionType = exception[1];
    message = exception[2] || exceptionType;
  } else if (genericError) {
    message = genericError[1];
  }

  const stacktrace = blockLines.slice(1).join("\n").trim();
  const source = extractSource(message, stacktrace);
  file ||= source.file;
  line ??= source.line;
  const topUserFrame = findTopUserFrame(stacktrace);
  const normalizedStack = stacktrace.split(/\r?\n/).map((part) => part.trim().replace(/\s+/g, " ")).filter(Boolean).join("\n");
  const fingerprint = createHash("sha256")
    .update([exceptionType, normalizeMessage(message), file, line ?? "", topUserFrame, normalizedStack].join("\n"))
    .digest("hex")
    .slice(0, 24);
  return {
    id: `LOG-${fingerprint.toUpperCase()}`,
    severity: "error",
    category,
    exception_type: exceptionType,
    message: message.trim(),
    file,
    line,
    top_user_frame: topUserFrame,
    stacktrace,
    fingerprint
  };
}

export async function readSourceSnippet(repositoryRoot, fileName, lineNumber, radius = 5) {
  if (!fileName || !Number.isSafeInteger(lineNumber) || lineNumber < 1) return null;
  const root = path.resolve(repositoryRoot);
  const normalized = fileName.replaceAll("\\", "/").replace(/^\/+/, "");
  const absolutePath = path.resolve(root, normalized);
  const relative = path.relative(root, absolutePath);
  if (relative.startsWith("..") || path.isAbsolute(relative)) return null;
  try {
    const source = await readFile(absolutePath, "utf8");
    const lines = source.split(/\r?\n/);
    const start = Math.max(1, lineNumber - radius);
    const end = Math.min(lines.length, lineNumber + radius);
    return {
      file: relative.replaceAll(path.sep, "/"),
      start_line: start,
      end_line: end,
      lines: lines.slice(start - 1, end).map((text, index) => ({
        number: start + index,
        text,
        target: start + index === lineNumber
      }))
    };
  } catch {
    return null;
  }
}

export function deduplicateBlocks(blocks) {
  const grouped = new Map();
  for (const block of blocks) {
    const parsed = parseBlock(block);
    const existing = grouped.get(parsed.fingerprint);
    if (existing) {
      existing.count += 1;
      existing.last_seen_index = block.index;
    } else {
      grouped.set(parsed.fingerprint, { ...parsed, count: 1, first_seen_index: block.index, last_seen_index: block.index });
    }
  }
  return [...grouped.values()].sort((a, b) => b.count - a.count || a.first_seen_index - b.first_seen_index);
}

export function collectErrorBlocks(text, maxLines = 5000) {
  const lines = String(text).split(/\r?\n/);
  const tail = lines.length > maxLines ? lines.slice(-maxLines) : lines;
  const blocks = [];
  for (let i = 0; i < tail.length; i += 1) {
    if (!isHeader(tail[i])) continue;
    const block = [tail[i]];
    let j = i + 1;
    while (j < tail.length && tail[j].trim() && !isHeader(tail[j])) {
      block.push(tail[j]);
      j += 1;
      if (block.length >= 80) break;
    }
    blocks.push({ index: i + 1, lines: block });
    i = j - 1;
  }
  return deduplicateBlocks(blocks);
}

export async function summarizeEditorLog(filePath, {
  repositoryRoot = process.cwd(),
  maxLines = 5000,
  maxItems = 40,
  includeSource = false,
  maxBytes = DEFAULT_TAIL_BYTES
} = {}) {
  const info = await stat(filePath);
  const content = await readTail(filePath, maxBytes);
  const grouped = collectErrorBlocks(content, maxLines).slice(0, maxItems);
  if (includeSource) {
    for (const item of grouped) {
      item.source = await readSourceSnippet(repositoryRoot, item.file, item.line);
    }
  }
  return {
    status: "ok",
    log_file: path.resolve(filePath),
    bytes_scanned: Math.min(info.size, maxBytes),
    error_groups: grouped.length,
    total_occurrences: grouped.reduce((sum, item) => sum + item.count, 0),
    truncated: info.size > maxBytes || content.split(/\r?\n/).length > maxLines,
    errors: grouped
  };
}

export function defaultEditorLogPath(platform = process.platform, home = process.env.HOME || process.env.USERPROFILE || "") {
  if (process.env.UNITY_EDITOR_LOG) return process.env.UNITY_EDITOR_LOG;
  if (platform === "win32") {
    const local = process.env.LOCALAPPDATA || path.join(home, "AppData", "Local");
    return path.join(local, "Unity", "Editor", "Editor.log");
  }
  if (platform === "darwin") return path.join(home, "Library", "Logs", "Unity", "Editor.log");
  return path.join(home, ".config", "unity3d", "Editor.log");
}
