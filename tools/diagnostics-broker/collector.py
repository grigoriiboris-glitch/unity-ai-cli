from __future__ import annotations

import re
from dataclasses import dataclass
from typing import Iterable

from broker import DiagnosticEvent


_EXCEPTION_RE = re.compile(
    r"^(?P<exception>[A-Za-z_][A-Za-z0-9_.]*Exception):?\s*(?P<message>.*)$"
)
_ERROR_RE = re.compile(r"^(?:Error|ERROR|error):\s*(?P<message>.+)$")


@dataclass(frozen=True)
class CollectedBlock:
    event: DiagnosticEvent
    raw_text: str


class UnityLogCollector:
    """Small deterministic parser for Unity exception/error blocks.

    It intentionally does not expose the full source log. Raw text may be persisted by
    the caller while only the normalized event is forwarded to the broker.
    """

    def collect(
        self,
        text: str,
        *,
        task_id: str = "",
        run_id: str = "",
        git_commit: str = "",
        raw_log_path: str = "",
    ) -> list[CollectedBlock]:
        lines = text.splitlines()
        blocks: list[CollectedBlock] = []
        index = 0

        while index < len(lines):
            line = lines[index].strip()
            exception_match = _EXCEPTION_RE.match(line)
            error_match = _ERROR_RE.match(line)

            if not exception_match and not error_match:
                index += 1
                continue

            block_lines = [lines[index]]
            index += 1
            while index < len(lines):
                candidate = lines[index]
                if _EXCEPTION_RE.match(candidate.strip()) or _ERROR_RE.match(candidate.strip()):
                    break
                if not candidate.strip() and len(block_lines) > 1:
                    break
                block_lines.append(candidate)
                index += 1

            raw = "\n".join(block_lines)
            if exception_match:
                exception_type = exception_match.group("exception")
                message = exception_match.group("message") or exception_type
                severity = "error"
                category = "exception"
            else:
                exception_type = ""
                message = error_match.group("message")
                severity = "error"
                category = "log"

            stacktrace = "\n".join(block_lines[1:]).strip()
            top_frame = self._top_frame(stacktrace)
            source_file, source_line = self._source(stacktrace)

            blocks.append(
                CollectedBlock(
                    event=DiagnosticEvent(
                        severity=severity,
                        category=category,
                        message=message,
                        exception_type=exception_type,
                        file=source_file,
                        line=source_line,
                        top_user_frame=top_frame,
                        stacktrace=stacktrace,
                        task_id=task_id,
                        run_id=run_id,
                        git_commit=git_commit,
                        raw_log_path=raw_log_path,
                    ),
                    raw_text=raw,
                )
            )

        return blocks

    @staticmethod
    def _top_frame(stacktrace: str) -> str:
        for line in stacktrace.splitlines():
            value = line.strip()
            if value.startswith("at "):
                frame = value[3:]
                if not frame.startswith(("UnityEngine.", "UnityEditor.", "System.", "Microsoft.")):
                    return frame.split(" in ", 1)[0]
        return ""

    @staticmethod
    def _source(stacktrace: str) -> tuple[str, int | None]:
        for line in stacktrace.splitlines():
            match = re.search(r"(.+?):(\d+)(?::\d+)?$", line.strip())
            if match:
                return match.group(1), int(match.group(2))
        return "", None
