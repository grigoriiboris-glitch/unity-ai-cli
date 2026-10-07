#!/usr/bin/env python3
from __future__ import annotations

import hashlib
import json
import re
import sqlite3
import time
from dataclasses import asdict, dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Iterable, Optional

DEFAULT_MAX_DIAGNOSTICS = 20
DEFAULT_MAX_CHARS = 3000

_UUID_RE = re.compile(r"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[1-5][0-9a-fA-F]{3}-[89abAB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}\b")
_HEX_RE = re.compile(r"\b0x[0-9a-fA-F]+\b")
_ISO_TIME_RE = re.compile(r"\b\d{4}-\d{2}-\d{2}[T ]\d{2}:\d{2}:\d{2}(?:\.\d+)?(?:Z|[+-]\d{2}:?\d{2})?\b")
_NUMBER_RE = re.compile(r"(?<![A-Za-z_])[-+]?\d+(?:\.\d+)?(?![A-Za-z_])")
_FRAME_RE = re.compile(r"^\s*at\s+(.+?)\s*(?:\((.*)\))?\s*$")
_PATH_LINE_RE = re.compile(r"(.*?):(\d+)(?::\d+)?$")


@dataclass(frozen=True)
class DiagnosticEvent:
    severity: str
    category: str
    message: str
    exception_type: str = ""
    file: str = ""
    line: Optional[int] = None
    top_user_frame: str = ""
    stacktrace: str = ""
    task_id: str = ""
    run_id: str = ""
    git_commit: str = ""
    raw_log_path: str = ""
    timestamp: str = ""


class DiagnosticsBroker:
    def __init__(self, db_path: str | Path):
        self.db_path = str(db_path)
        Path(self.db_path).parent.mkdir(parents=True, exist_ok=True)
        self._init_db()

    def _connect(self) -> sqlite3.Connection:
        connection = sqlite3.connect(self.db_path)
        connection.row_factory = sqlite3.Row
        connection.execute("PRAGMA journal_mode=WAL")
        connection.execute("PRAGMA foreign_keys=ON")
        return connection

    def _init_db(self) -> None:
        with self._connect() as db:
            db.executescript(
                """
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
                    task_id TEXT NOT NULL,
                    run_id TEXT NOT NULL,
                    git_commit TEXT,
                    seen_at TEXT NOT NULL
                );

                CREATE INDEX IF NOT EXISTS idx_diag_last_seen
                    ON diagnostics(last_seen DESC);
                CREATE INDEX IF NOT EXISTS idx_diag_task
                    ON diagnostic_occurrences(task_id);
                CREATE INDEX IF NOT EXISTS idx_diag_run
                    ON diagnostic_occurrences(run_id);

                CREATE TABLE IF NOT EXISTS task_sequences (
                    utc_date TEXT PRIMARY KEY,
                    next_number INTEGER NOT NULL
                );
                """
            )

    @staticmethod
    def now_iso() -> str:
        return datetime.now(timezone.utc).isoformat(timespec="milliseconds")

    def new_task_id(self, now: Optional[datetime] = None) -> str:
        instant = now or datetime.now(timezone.utc)
        date_key = instant.astimezone(timezone.utc).strftime("%Y%m%d")
        with self._connect() as db:
            row = db.execute(
                "SELECT next_number FROM task_sequences WHERE utc_date = ?",
                (date_key,),
            ).fetchone()
            if row is None:
                number = 1
                db.execute(
                    "INSERT INTO task_sequences (utc_date, next_number) VALUES (?, ?)",
                    (date_key, 2),
                )
            else:
                number = int(row["next_number"])
                db.execute(
                    "UPDATE task_sequences SET next_number = ? WHERE utc_date = ?",
                    (number + 1, date_key),
                )
            return f"TASK-{date_key}-{number:03d}"

    @staticmethod
    def normalize_message(message: str) -> str:
        normalized = message or ""
        normalized = _ISO_TIME_RE.sub("<time>", normalized)
        normalized = _UUID_RE.sub("<uuid>", normalized)
        normalized = _HEX_RE.sub("<hex>", normalized)
        normalized = _NUMBER_RE.sub("<n>", normalized)
        normalized = re.sub(r"\s+", " ", normalized).strip()
        return normalized

    @staticmethod
    def extract_source(message: str, stacktrace: str) -> tuple[str, Optional[int]]:
        for source in (message, stacktrace):
            for line in source.splitlines():
                match = _PATH_LINE_RE.search(line.strip())
                if match:
                    path, number = match.groups()
                    return path, int(number)
        return "", None

    @staticmethod
    def find_top_user_frame(stacktrace: str) -> str:
        if not stacktrace:
            return ""

        ignored = (
            "UnityEngine.",
            "UnityEditor.",
            "System.",
            "Microsoft.",
            "Mono.",
            "mscorlib",
        )
        for raw_line in stacktrace.splitlines():
            match = _FRAME_RE.match(raw_line)
            frame = match.group(1) if match else raw_line.strip()
            if not frame:
                continue
            if not frame.startswith(ignored):
                return frame
        return ""

    @classmethod
    def fingerprint_for(cls, event: DiagnosticEvent) -> str:
        source_file = event.file or cls.extract_source(event.message, event.stacktrace)[0]
        source_line = str(event.line if event.line is not None else cls.extract_source(event.message, event.stacktrace)[1] or "")
        top_frame = event.top_user_frame or cls.find_top_user_frame(event.stacktrace)
        payload = "\n".join(
            (
                (event.exception_type or "").strip(),
                cls.normalize_message(event.message),
                source_file.strip(),
                source_line,
                top_frame.strip(),
            )
        )
        return hashlib.sha256(payload.encode("utf-8")).hexdigest()[:24]

    @staticmethod
    def diagnostic_id(fingerprint: str) -> str:
        return f"DIAG-{fingerprint.upper()}"

    def ingest(self, event: DiagnosticEvent) -> str:
        timestamp = event.timestamp or self.now_iso()
        fingerprint = self.fingerprint_for(event)
        diagnostic_id = self.diagnostic_id(fingerprint)

        source_file = event.file
        source_line = event.line
        if not source_file or source_line is None:
            inferred_file, inferred_line = self.extract_source(event.message, event.stacktrace)
            source_file = source_file or inferred_file
            source_line = source_line if source_line is not None else inferred_line

        top_frame = event.top_user_frame or self.find_top_user_frame(event.stacktrace)
        message = event.message.strip()
        severity = event.severity.lower().strip() or "error"

        with self._connect() as db:
            existing = db.execute(
                "SELECT count FROM diagnostics WHERE fingerprint = ?",
                (fingerprint,),
            ).fetchone()

            if existing is None:
                db.execute(
                    """
                    INSERT INTO diagnostics (
                        id, fingerprint, severity, category, message, file, line,
                        count, first_seen, last_seen, task_id, git_commit,
                        raw_log_path, exception_type, top_user_frame, stacktrace
                    )
                    VALUES (?, ?, ?, ?, ?, ?, ?, 1, ?, ?, ?, ?, ?, ?, ?, ?)
                    """,
                    (
                        diagnostic_id,
                        fingerprint,
                        severity,
                        event.category,
                        message,
                        source_file,
                        source_line,
                        timestamp,
                        timestamp,
                        event.task_id,
                        event.git_commit,
                        event.raw_log_path,
                        event.exception_type,
                        top_frame,
                        event.stacktrace,
                    ),
                )
            else:
                db.execute(
                    """
                    UPDATE diagnostics
                    SET count = count + 1,
                        last_seen = ?,
                        severity = ?,
                        category = ?,
                        message = ?,
                        file = ?,
                        line = ?,
                        task_id = ?,
                        git_commit = ?,
                        raw_log_path = ?,
                        exception_type = ?,
                        top_user_frame = ?,
                        stacktrace = ?
                    WHERE fingerprint = ?
                    """,
                    (
                        timestamp,
                        severity,
                        event.category,
                        message,
                        source_file,
                        source_line,
                        event.task_id,
                        event.git_commit,
                        event.raw_log_path,
                        event.exception_type,
                        top_frame,
                        event.stacktrace,
                        fingerprint,
                    ),
                )

            if event.task_id or event.run_id:
                db.execute(
                    """
                    INSERT INTO diagnostic_occurrences
                        (diagnostic_id, task_id, run_id, git_commit, seen_at)
                    VALUES (?, ?, ?, ?, ?)
                    """,
                    (
                        diagnostic_id,
                        event.task_id,
                        event.run_id,
                        event.git_commit,
                        timestamp,
                    ),
                )

        return diagnostic_id

    def _query_rows(
        self,
        *,
        task_id: str = "",
        query: str = "",
        since: str = "",
        limit: int = DEFAULT_MAX_DIAGNOSTICS,
        offset: int = 0,
    ) -> list[sqlite3.Row]:
        clauses: list[str] = []
        params: list[Any] = []
        joins = ""

        if task_id:
            joins = "JOIN diagnostic_occurrences occ ON occ.diagnostic_id = d.id"
            clauses.append("occ.task_id = ?")
            params.append(task_id)

        if query:
            clauses.append(
                "(d.id LIKE ? OR d.fingerprint LIKE ? OR d.message LIKE ? OR d.file LIKE ? OR d.exception_type LIKE ?)"
            )
            needle = f"%{query}%"
            params.extend([needle] * 5)

        if since:
            clauses.append("d.last_seen >= ?")
            params.append(since)

        where = f"WHERE {' AND '.join(clauses)}" if clauses else ""
        sql = f"""
            SELECT DISTINCT d.*
            FROM diagnostics d
            {joins}
            {where}
            ORDER BY d.last_seen DESC
            LIMIT ? OFFSET ?
        """
        params.extend([max(1, int(limit)), max(0, int(offset))])

        with self._connect() as db:
            return list(db.execute(sql, params).fetchall())

    @staticmethod
    def _summary_row(row: sqlite3.Row) -> dict[str, Any]:
        return {
            "id": row["id"],
            "fingerprint": row["fingerprint"],
            "severity": row["severity"],
            "category": row["category"],
            "message": row["message"],
            "file": row["file"],
            "line": row["line"],
            "count": row["count"],
            "first_seen": row["first_seen"],
            "last_seen": row["last_seen"],
            "task_id": row["task_id"],
            "git_commit": row["git_commit"],
        }

    def diagnostics_summary(
        self,
        *,
        task_id: str = "",
        page: int = 1,
        page_size: int = DEFAULT_MAX_DIAGNOSTICS,
        max_chars: int = DEFAULT_MAX_CHARS,
    ) -> dict[str, Any]:
        page = max(1, int(page))
        page_size = max(1, min(int(page_size), DEFAULT_MAX_DIAGNOSTICS))
        rows = self._query_rows(
            task_id=task_id,
            limit=page_size + 1,
            offset=(page - 1) * page_size,
        )
        has_more = len(rows) > page_size
        rows = rows[:page_size]

        result = {
            "status": "ok",
            "count": len(rows),
            "page": page,
            "page_size": page_size,
            "has_more": has_more,
            "diagnostics": [self._summary_row(row) for row in rows],
        }
        return self._bounded_json(result, max_chars)

    def diagnostics_search(
        self,
        query: str,
        *,
        page: int = 1,
        page_size: int = DEFAULT_MAX_DIAGNOSTICS,
        max_chars: int = DEFAULT_MAX_CHARS,
    ) -> dict[str, Any]:
        page = max(1, int(page))
        page_size = max(1, min(int(page_size), DEFAULT_MAX_DIAGNOSTICS))
        rows = self._query_rows(
            query=query,
            limit=page_size + 1,
            offset=(page - 1) * page_size,
        )
        has_more = len(rows) > page_size
        rows = rows[:page_size]
        return self._bounded_json(
            {
                "status": "ok",
                "query": query,
                "count": len(rows),
                "page": page,
                "page_size": page_size,
                "has_more": has_more,
                "diagnostics": [self._summary_row(row) for row in rows],
            },
            max_chars,
        )

    def diagnostics_detail(self, diagnostic_id: str) -> dict[str, Any]:
        with self._connect() as db:
            row = db.execute(
                "SELECT * FROM diagnostics WHERE id = ?",
                (diagnostic_id,),
            ).fetchone()

        if row is None:
            return {"status": "not_found", "id": diagnostic_id}

        data = self._summary_row(row)
        data.update(
            {
                "exception_type": row["exception_type"],
                "top_user_frame": row["top_user_frame"],
                "raw_log_path": row["raw_log_path"],
                "stacktrace": row["stacktrace"],
            }
        )
        return {"status": "ok", "diagnostic": data}

    def diagnostics_since(
        self,
        task_id: str,
        *,
        max_chars: int = DEFAULT_MAX_CHARS,
    ) -> dict[str, Any]:
        rows = self._query_rows(
            task_id=task_id,
            limit=DEFAULT_MAX_DIAGNOSTICS + 1,
            offset=0,
        )
        has_more = len(rows) > DEFAULT_MAX_DIAGNOSTICS
        rows = rows[:DEFAULT_MAX_DIAGNOSTICS]
        return self._bounded_json(
            {
                "status": "ok",
                "task_id": task_id,
                "count": len(rows),
                "has_more": has_more,
                "diagnostics": [self._summary_row(row) for row in rows],
            },
            max_chars,
        )

    def diagnostics_clear(self, task_id: str) -> dict[str, Any]:
        with self._connect() as db:
            cursor = db.execute(
                "DELETE FROM diagnostic_occurrences WHERE task_id = ?",
                (task_id,),
            )
            return {"status": "ok", "task_id": task_id, "cleared_occurrences": cursor.rowcount}

    @staticmethod
    def _bounded_json(value: dict[str, Any], max_chars: int) -> dict[str, Any]:
        payload = json.dumps(value, ensure_ascii=False, separators=(",", ":"))
        if len(payload) <= max_chars:
            return value

        diagnostics = list(value.get("diagnostics", []))
        while diagnostics:
            diagnostics.pop()
            candidate = dict(value)
            candidate["diagnostics"] = diagnostics
            candidate["count"] = len(diagnostics)
            candidate["truncated"] = True
            if len(json.dumps(candidate, ensure_ascii=False, separators=(",", ":"))) <= max_chars:
                return candidate

        return {
            "status": "truncated",
            "count": 0,
            "has_more": bool(value.get("count")),
            "diagnostics": [],
        }


def event_from_mapping(data: dict[str, Any]) -> DiagnosticEvent:
    return DiagnosticEvent(
        severity=str(data.get("severity", "error")),
        category=str(data.get("category", "")),
        message=str(data.get("message", "")),
        exception_type=str(data.get("exception_type", "")),
        file=str(data.get("file", "")),
        line=int(data["line"]) if data.get("line") is not None else None,
        top_user_frame=str(data.get("top_user_frame", "")),
        stacktrace=str(data.get("stacktrace", "")),
        task_id=str(data.get("task_id", "")),
        run_id=str(data.get("run_id", "")),
        git_commit=str(data.get("git_commit", "")),
        raw_log_path=str(data.get("raw_log_path", "")),
        timestamp=str(data.get("timestamp", "")),
    )


def load_json_lines(path: str | Path) -> Iterable[DiagnosticEvent]:
    with Path(path).open("r", encoding="utf-8") as stream:
        for raw_line in stream:
            line = raw_line.strip()
            if not line:
                continue
            yield event_from_mapping(json.loads(line))
